using System.Globalization;
using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.DTOs.Payments.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.Payments;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class PaymentService : IPaymentService
{
    private const string PendingStatus = "PENDING";
    private const string ProcessingStatus = "PROCESSING";
    private const string PaidStatus = "PAID";
    private const string CancelledStatus = "CANCELLED";
    private const string UnderpaidStatus = "UNDERPAID";
    private const string ExpiredStatus = "EXPIRED";
    private const string FailedStatus = "FAILED";
    private const string PayOsProvider = "payOS";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOSService _payOsService;
    private readonly IServiceCreditService _serviceCreditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IPayOSService payOsService,
        IServiceCreditService serviceCreditService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PaymentService> logger)
    {
        _unitOfWork = unitOfWork;
        _payOsService = payOsService;
        _serviceCreditService = serviceCreditService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<PayOSReturnResponse> ProcessPayOSReturnAsync(
        IReadOnlyDictionary<string, string> queryParameters,
        CancellationToken cancellationToken = default)
    {
        return await BuildPayOSRedirectStatusResponseAsync(queryParameters, cancellationToken);
    }

    public async Task<PayOSReturnResponse> ProcessPayOSCancelAsync(
        IReadOnlyDictionary<string, string> queryParameters,
        CancellationToken cancellationToken = default)
    {
        return await BuildPayOSRedirectStatusResponseAsync(queryParameters, cancellationToken);
    }

    public async Task<bool> ProcessPayOSWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        var callback = await _payOsService.VerifyWebhookAsync(rawBody, cancellationToken);
        if (!callback.IsValid)
        {
            return false;
        }

        if (!callback.IsPaid && !callback.IsCancelled)
        {
            return true;
        }

        var transactionReference = callback.OrderCode.ToString(CultureInfo.InvariantCulture);
        var verifiedState = CreateWebhookVerifiedState(callback);

        _unitOfWork.ClearTrackedChanges();
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            var transaction = await _unitOfWork.PaymentTransactions
                .GetByTransactionReferenceForUpdateAsync(
                    transactionReference,
                    cancellationToken);
            var validationError = ValidateLockedTransaction(
                transaction,
                callback.OrderCode,
                verifiedState,
                currentUserId: null);
            if (validationError != PaymentReconciliationErrorCode.None)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return false;
            }

            var mutationError = await ApplyVerifiedPayOSStateAsync(
                transaction!,
                verifiedState,
                DateTime.UtcNow,
                cancellationToken);
            if (mutationError != PaymentReconciliationErrorCode.None)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return false;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            _logger.LogWarning(
                "payOS webhook processing failed for order {OrderCode}; category {ErrorCategory}.",
                callback.OrderCode,
                ex.GetType().Name);
            return false;
        }
    }

    public async Task<PayOSPaymentStatusResponse?> GetPayOSPaymentStatusAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        if (orderCode <= 0)
        {
            return null;
        }

        var transaction = await _unitOfWork.PaymentTransactions.GetByTransactionReferenceAsync(
            orderCode.ToString(CultureInfo.InvariantCulture),
            cancellationToken);

        if (transaction is null || transaction.Payment is null)
        {
            return null;
        }

        return BuildPaymentStatusResponse(transaction, orderCode);
    }

    public async Task<PaymentReconciliationResult<PayOSPaymentStatusResponse>>
        ReconcilePayOSPaymentAsync(
            long orderCode,
            CancellationToken cancellationToken = default)
    {
        if (orderCode <= 0)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.InvalidRequest,
                "Invalid orderCode.");
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.Unauthenticated,
                "Authentication is required.");
        }

        return await ReconcilePayOSPaymentCoreAsync(
            orderCode,
            currentUserId.Value,
            cancelPendingAtProvider: false,
            cancellationReason: null,
            cancellationToken);
    }

    public async Task<PaymentReconciliationResult<PayOSPaymentStatusResponse>>
        CancelPendingPayOSCheckoutAsync(
            Guid userSubscriptionId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        if (userSubscriptionId == Guid.Empty || userId == Guid.Empty)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.InvalidRequest,
                "Invalid pending checkout cancellation request.");
        }

        var transaction = await _unitOfWork.PaymentTransactions
            .GetLatestPayOSByUserSubscriptionIdAsync(
                userSubscriptionId,
                cancellationToken);
        if (transaction is null
            || transaction.UserSubscriptionId != userSubscriptionId
            || transaction.Payment is null
            || transaction.Payment.UserSubscription is null
            || transaction.Payment.UserSubscriptionId != userSubscriptionId)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.NotFound,
                "Pending payOS checkout was not found.");
        }

        if (transaction.UserId != userId
            || transaction.Payment.UserId != userId
            || transaction.Payment.UserSubscription.UserId != userId)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.Forbidden,
                "Pending checkout does not belong to the current user.");
        }

        if (transaction.Payment.UserSubscription.Status != SubscriptionStatus.Pending)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.Conflict,
                "Only pending checkouts can be cancelled.");
        }

        if (!TryParseOrderCode(transaction.TransactionReference, out var orderCode))
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.Conflict,
                "Pending checkout has an invalid payOS order code.");
        }

        var reconciliation = await ReconcilePayOSPaymentCoreAsync(
            orderCode,
            userId,
            cancelPendingAtProvider: true,
            cancellationReason: "Cancelled by user.",
            cancellationToken);
        if (!reconciliation.Success || reconciliation.Data is null)
        {
            return reconciliation;
        }

        var data = reconciliation.Data;
        if (data.IsPaid || data.IsActive)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.Conflict,
                "Payment already completed; the package is active and cannot be cancelled.");
        }

        if (data.IsCancelled
            || string.Equals(data.PaymentStatus, PaymentStatus.Failed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return reconciliation;
        }

        return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
            PaymentReconciliationErrorCode.Conflict,
            "payOS has not confirmed that this checkout is closed. Please try again later.");
    }

    public async Task<PayOSPendingReconciliationSummary> ReconcilePendingPayOSPaymentsAsync(
        PayOSPendingReconciliationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ValidatePendingReconciliationSettings(settings);

        var utcNow = DateTime.UtcNow;
        var candidates = await _unitOfWork.PaymentTransactions.GetPendingPayOSCandidatesAsync(
            utcNow.AddMinutes(-settings.MinimumAgeMinutes),
            settings.BatchSize,
            cancellationToken);

        var paidCount = 0;
        var cancelledCount = 0;
        var failedCount = 0;
        var stillPendingCount = 0;
        var providerUnavailableCount = 0;
        var rateLimitedCount = 0;
        var invalidCount = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryParseOrderCode(candidate.TransactionReference, out var orderCode))
            {
                invalidCount++;
                _logger.LogWarning(
                    "Skipping pending payOS transaction {PaymentTransactionId} because its order code is invalid.",
                    candidate.PaymentTransactionId);
                continue;
            }

            var staleAt = candidate.CreatedAt.AddMinutes(
                settings.PaymentLinkExpirationMinutes + settings.CleanupGraceMinutes);
            var cancelPendingAtProvider = DateTime.UtcNow >= staleAt;

            PaymentReconciliationResult<PayOSPaymentStatusResponse> reconciliation;
            try
            {
                reconciliation = await ReconcilePayOSPaymentCoreAsync(
                    orderCode,
                    currentUserId: null,
                    cancelPendingAtProvider,
                    cancellationReason: "MediMate payment window expired.",
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                invalidCount++;
                _logger.LogWarning(
                    "Pending payOS maintenance failed for order {OrderCode}, transaction {PaymentTransactionId}, payment {PaymentId}, subscription {UserSubscriptionId}; category {ErrorCategory}.",
                    orderCode,
                    candidate.PaymentTransactionId,
                    candidate.PaymentId,
                    candidate.UserSubscriptionId,
                    ex.GetType().Name);
                continue;
            }

            if (!reconciliation.Success || reconciliation.Data is null)
            {
                switch (reconciliation.Error)
                {
                    case PaymentReconciliationErrorCode.ProviderRateLimited:
                        rateLimitedCount++;
                        break;
                    case PaymentReconciliationErrorCode.ProviderUnavailable:
                        providerUnavailableCount++;
                        break;
                    default:
                        invalidCount++;
                        break;
                }

                if (reconciliation.Error == PaymentReconciliationErrorCode.ProviderRateLimited)
                {
                    break;
                }

                continue;
            }

            var data = reconciliation.Data;
            if (data.IsPaid && data.IsActive)
            {
                paidCount++;
            }
            else if (data.IsCancelled)
            {
                cancelledCount++;
            }
            else if (string.Equals(
                         data.PaymentStatus,
                         PaymentStatus.Failed.ToString(),
                         StringComparison.OrdinalIgnoreCase))
            {
                failedCount++;
            }
            else
            {
                stillPendingCount++;
            }
        }

        return new PayOSPendingReconciliationSummary(
            candidates.Count,
            paidCount,
            cancelledCount,
            failedCount,
            stillPendingCount,
            providerUnavailableCount,
            rateLimitedCount,
            invalidCount);
    }

    private async Task<PaymentReconciliationResult<PayOSPaymentStatusResponse>>
        ReconcilePayOSPaymentCoreAsync(
            long orderCode,
            Guid? currentUserId,
            bool cancelPendingAtProvider,
            string? cancellationReason,
            CancellationToken cancellationToken)
    {
        if (orderCode <= 0)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.InvalidRequest,
                "Invalid orderCode.");
        }

        var transactionReference = orderCode.ToString(CultureInfo.InvariantCulture);
        PaymentTransaction? snapshot;
        try
        {
            snapshot = await _unitOfWork.PaymentTransactions.GetByTransactionReferenceAsync(
                transactionReference,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Local payment lookup failed for order {OrderCode}; category {ErrorCategory}.",
                orderCode,
                ex.GetType().Name);
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.Conflict,
                "Payment state could not be verified.");
        }

        var localError = ValidateLocalSnapshot(snapshot, orderCode, currentUserId);
        if (localError != PaymentReconciliationErrorCode.None)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                localError,
                BuildReconciliationFailureMessage(localError));
        }

        if (HasCompletePaidState(snapshot!))
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Ok(
                BuildPaymentStatusResponse(snapshot!, orderCode));
        }

        PayOSPaymentLinkLookupResult lookup;
        try
        {
            // Provider I/O deliberately occurs before opening the database transaction.
            lookup = await _payOsService.GetPaymentLinkAsync(orderCode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "payOS reconciliation lookup failed for order {OrderCode}; category {ErrorCategory}.",
                orderCode,
                ex.GetType().Name);
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.ProviderUnavailable,
                "payOS is temporarily unavailable.");
        }

        if (!lookup.Success || lookup.Data is null)
        {
            var lookupError = MapLookupError(lookup.Error);
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                lookupError,
                BuildReconciliationFailureMessage(lookupError));
        }

        var providerState = lookup.Data;
        var providerValidationError = ValidateProviderAgainstLocalSnapshot(
            providerState,
            snapshot!,
            orderCode);
        if (providerValidationError != PaymentReconciliationErrorCode.None)
        {
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                providerValidationError,
                BuildReconciliationFailureMessage(providerValidationError));
        }

        if (cancelPendingAtProvider
            && string.Equals(providerState.Status, PendingStatus, StringComparison.Ordinal))
        {
            PayOSPaymentLinkLookupResult cancellation;
            try
            {
                // Provider cancellation is deliberately completed before opening the database transaction.
                cancellation = await _payOsService.CancelPaymentLinkAsync(
                    orderCode,
                    cancellationReason,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "payOS cancellation failed for order {OrderCode}; category {ErrorCategory}.",
                    orderCode,
                    ex.GetType().Name);
                return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                    PaymentReconciliationErrorCode.ProviderUnavailable,
                    "payOS is temporarily unavailable.");
            }

            if (!cancellation.Success || cancellation.Data is null)
            {
                var cancellationError = MapLookupError(cancellation.Error);
                if (cancellationError == PaymentReconciliationErrorCode.ProviderRateLimited)
                {
                    return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                        cancellationError,
                        BuildReconciliationFailureMessage(cancellationError));
                }

                // Cancellation may race with payment completion. Re-read provider truth
                // before deciding that the local checkout must remain pending.
                var refreshed = await _payOsService.GetPaymentLinkAsync(
                    orderCode,
                    cancellationToken);
                if (!refreshed.Success || refreshed.Data is null)
                {
                    return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                        cancellationError,
                        BuildReconciliationFailureMessage(cancellationError));
                }

                providerState = refreshed.Data;
            }
            else
            {
                providerState = cancellation.Data;
            }

            providerValidationError = ValidateProviderAgainstLocalSnapshot(
                providerState,
                snapshot!,
                orderCode);
            if (providerValidationError != PaymentReconciliationErrorCode.None)
            {
                return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                    providerValidationError,
                    BuildReconciliationFailureMessage(providerValidationError));
            }
        }

        _unitOfWork.ClearTrackedChanges();
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            var transaction = await _unitOfWork.PaymentTransactions
                .GetByTransactionReferenceForUpdateAsync(
                    transactionReference,
                    cancellationToken);
            var lockedValidationError = ValidateLockedTransaction(
                transaction,
                orderCode,
                providerState,
                currentUserId);
            if (lockedValidationError != PaymentReconciliationErrorCode.None)
            {
                return await RollbackReconciliationFailureAsync(
                    lockedValidationError,
                    BuildReconciliationFailureMessage(lockedValidationError));
            }

            var mutationError = await ApplyVerifiedPayOSStateAsync(
                transaction!,
                providerState,
                DateTime.UtcNow,
                cancellationToken);
            if (mutationError != PaymentReconciliationErrorCode.None)
            {
                return await RollbackReconciliationFailureAsync(
                    mutationError,
                    BuildReconciliationFailureMessage(mutationError));
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Ok(
                BuildPaymentStatusResponse(transaction!, orderCode, providerState));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            _logger.LogWarning(
                "Payment reconciliation failed for order {OrderCode}; category {ErrorCategory}.",
                orderCode,
                ex.GetType().Name);
            return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(
                PaymentReconciliationErrorCode.Conflict,
                "Payment reconciliation could not be completed.");
        }
    }

    public async Task<PagedResponse<PaymentResponse>> GetAllPaymentsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paged = await _unitOfWork.Payments.GetPagedWithSubscriptionAsync(
            pageNumber,
            pageSize,
            cancellationToken);

        return MapToPagedResponse(paged);
    }

    public async Task<PagedResponse<PaymentResponse>> GetPaymentsByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return CreateEmptyPagedResponse(pageNumber, pageSize);
        }

        var paged = await _unitOfWork.Payments.GetPagedByUserIdWithSubscriptionAsync(
            userId,
            pageNumber,
            pageSize,
            cancellationToken);

        return MapToPagedResponse(paged);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, PagedResponse<PaymentResponse>? Data)> GetMyPaymentsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return (false, new[] { "User is not authenticated." }, null);
        }

        var paged = await _unitOfWork.Payments.GetPagedByUserIdWithSubscriptionAsync(
            userId.Value,
            pageNumber,
            pageSize,
            cancellationToken);

        return (true, Array.Empty<string>(), MapToPagedResponse(paged));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, PaymentResponse? Data)> GetMyPaymentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid payment id." }, null);
        }

        var userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return (false, false, new[] { "User is not authenticated." }, null);
        }

        var payment = await _unitOfWork.Payments.GetByIdAndUserIdWithSubscriptionAsync(
            id,
            userId.Value,
            cancellationToken);

        if (payment is null || payment.IsDeleted)
        {
            return (false, true, Array.Empty<string>(), null);
        }

        return (true, false, Array.Empty<string>(), MapToResponse(payment));
    }

    public async Task<PaymentResponse?> GetPaymentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var payment = await _unitOfWork.Payments.GetByIdWithSubscriptionAsync(id, cancellationToken);
        if (payment is null || payment.IsDeleted)
        {
            return null;
        }

        return MapToResponse(payment);
    }

    private async Task<PayOSReturnResponse> BuildPayOSRedirectStatusResponseAsync(
        IReadOnlyDictionary<string, string> queryParameters,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrderCode(queryParameters, out var orderCode))
        {
            return new PayOSReturnResponse
            {
                Success = false,
                Message = "Invalid orderCode.",
            };
        }

        var status = await GetPayOSPaymentStatusAsync(orderCode, cancellationToken);

        if (status is null)
        {
            return new PayOSReturnResponse
            {
                Success = false,
                Message = "Payment transaction not found.",
                OrderCode = orderCode.ToString(CultureInfo.InvariantCulture),
            };
        }

        return new PayOSReturnResponse
        {
            Success = status.IsPaid && status.IsActive,
            Message = status.Message,
            PaymentId = status.PaymentId,
            SubscriptionId = status.SubscriptionId,
            OrderCode = status.OrderCode,
            Status = status.PaymentStatus,
            Cancelled = status.IsCancelled,
        };
    }

    private static PayOSPaymentLinkResult CreateWebhookVerifiedState(
        PayOSWebhookResult callback)
    {
        var status = callback.IsPaid ? PaidStatus : CancelledStatus;

        return new PayOSPaymentLinkResult
        {
            OrderCode = callback.OrderCode,
            PaymentLinkId = callback.PaymentLinkId ?? string.Empty,
            Amount = callback.Amount,
            AmountPaid = callback.IsPaid ? callback.Amount : 0,
            AmountRemaining = callback.IsPaid ? 0 : callback.Amount,
            Status = status,
            LatestTransactionReference = callback.Reference,
            LatestTransactionDescription = callback.Description,
            ResponseCode = callback.Code,
            RawResponse = callback.RawBody ?? string.Empty,
        };
    }

    private static PaymentReconciliationErrorCode ValidateLocalSnapshot(
        PaymentTransaction? transaction,
        long orderCode,
        Guid? currentUserId)
    {
        if (transaction is null
            || transaction.IsDeleted
            || transaction.Payment is null
            || transaction.Payment.IsDeleted
            || transaction.Payment.UserSubscription is null
            || transaction.Payment.UserSubscription.IsDeleted
            || transaction.Payment.UserSubscription.Plan is null
            || transaction.Payment.UserSubscription.Plan.IsDeleted)
        {
            return PaymentReconciliationErrorCode.NotFound;
        }

        if (!IsPayOsTransaction(transaction))
        {
            return PaymentReconciliationErrorCode.Conflict;
        }

        var payment = transaction.Payment;
        var subscription = payment.UserSubscription;
        if (currentUserId.HasValue
            && (transaction.UserId != currentUserId.Value
                || payment.UserId != currentUserId.Value
                || subscription.UserId != currentUserId.Value))
        {
            return PaymentReconciliationErrorCode.Forbidden;
        }

        var expectedReference = orderCode.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(
                transaction.TransactionReference,
                expectedReference,
                StringComparison.Ordinal))
        {
            return PaymentReconciliationErrorCode.OrderCodeMismatch;
        }

        if (transaction.PaymentId != payment.Id
            || transaction.UserSubscriptionId != subscription.Id
            || payment.UserSubscriptionId != subscription.Id)
        {
            return PaymentReconciliationErrorCode.Conflict;
        }

        return LocalAmountsMatch(transaction, payment)
            ? PaymentReconciliationErrorCode.None
            : PaymentReconciliationErrorCode.AmountMismatch;
    }

    private static PaymentReconciliationErrorCode ValidateProviderAgainstLocalSnapshot(
        PayOSPaymentLinkResult providerState,
        PaymentTransaction transaction,
        long orderCode)
    {
        if (providerState.OrderCode != orderCode)
        {
            return PaymentReconciliationErrorCode.OrderCodeMismatch;
        }

        var payment = transaction.Payment!;
        if (!TryConvertWholeVnd(payment.Amount, out var paymentAmount)
            || !TryConvertWholeVnd(transaction.Amount, out var transactionAmount)
            || providerState.Amount != paymentAmount
            || providerState.Amount != transactionAmount)
        {
            return PaymentReconciliationErrorCode.AmountMismatch;
        }

        if (providerState.Status == PaidStatus
            && providerState.AmountPaid < providerState.Amount)
        {
            return PaymentReconciliationErrorCode.AmountMismatch;
        }

        return PaymentReconciliationErrorCode.None;
    }

    private static PaymentReconciliationErrorCode ValidateLockedTransaction(
        PaymentTransaction? transaction,
        long orderCode,
        PayOSPaymentLinkResult providerState,
        Guid? currentUserId)
    {
        if (transaction is null
            || transaction.IsDeleted
            || transaction.Payment is null
            || transaction.Payment.IsDeleted
            || transaction.Payment.UserSubscription is null
            || transaction.Payment.UserSubscription.IsDeleted
            || transaction.Payment.UserSubscription.Plan is null
            || transaction.Payment.UserSubscription.Plan.IsDeleted)
        {
            return PaymentReconciliationErrorCode.NotFound;
        }

        if (!IsPayOsTransaction(transaction))
        {
            return PaymentReconciliationErrorCode.Conflict;
        }

        var payment = transaction.Payment;
        var subscription = payment.UserSubscription;
        if (currentUserId.HasValue
            && (transaction.UserId != currentUserId.Value
                || payment.UserId != currentUserId.Value
                || subscription.UserId != currentUserId.Value))
        {
            return PaymentReconciliationErrorCode.Forbidden;
        }

        if (providerState.OrderCode != orderCode
            || !string.Equals(
                transaction.TransactionReference,
                orderCode.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            return PaymentReconciliationErrorCode.OrderCodeMismatch;
        }

        if (transaction.PaymentId != payment.Id
            || transaction.UserSubscriptionId != subscription.Id
            || payment.UserSubscriptionId != subscription.Id)
        {
            return PaymentReconciliationErrorCode.Conflict;
        }

        var amountError = ValidateProviderAgainstLocalSnapshot(
            providerState,
            transaction,
            orderCode);
        if (amountError != PaymentReconciliationErrorCode.None)
        {
            return amountError;
        }

        return payment.Status == PaymentStatus.Refunded
            ? PaymentReconciliationErrorCode.Conflict
            : PaymentReconciliationErrorCode.None;
    }

    private static bool LocalAmountsMatch(
        PaymentTransaction transaction,
        Payment payment)
    {
        return TryConvertWholeVnd(payment.Amount, out var paymentAmount)
            && TryConvertWholeVnd(transaction.Amount, out var transactionAmount)
            && paymentAmount == transactionAmount;
    }

    private static bool TryConvertWholeVnd(decimal amount, out long value)
    {
        value = 0;
        if (amount <= 0
            || amount != decimal.Truncate(amount)
            || amount > long.MaxValue)
        {
            return false;
        }

        value = decimal.ToInt64(amount);
        return true;
    }

    private static bool HasCompletePaidState(PaymentTransaction transaction)
    {
        var payment = transaction.Payment!;
        var subscription = payment.UserSubscription;

        return payment.Status == PaymentStatus.Paid
            && payment.PaidAt.HasValue
            && string.Equals(transaction.Status, "Paid", StringComparison.Ordinal)
            && transaction.PaidAt.HasValue
            && transaction.ProcessedAt.HasValue
            && subscription.Status == SubscriptionStatus.Active
            && subscription.StartDate.HasValue;
    }

    private async Task<PaymentReconciliationErrorCode> ApplyVerifiedPayOSStateAsync(
        PaymentTransaction transaction,
        PayOSPaymentLinkResult providerState,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedProviderStatus(providerState.Status))
        {
            return PaymentReconciliationErrorCode.ProviderInvalidResponse;
        }

        UpdateProviderAudit(transaction, providerState, utcNow);

        switch (providerState.Status)
        {
            case PaidStatus:
                var grantStatus = await _serviceCreditService.GrantAsync(
                    transaction.UserSubscriptionId,
                    transaction.Payment!.Id,
                    transaction.UserId,
                    utcNow,
                    cancellationToken);
                if (grantStatus == QuotaMutationStatus.Rejected)
                {
                    return PaymentReconciliationErrorCode.Conflict;
                }

                ApplyPaidState(transaction, utcNow);
                break;
            case CancelledStatus:
                ApplyCancelledState(transaction, utcNow);
                break;
            case ExpiredStatus:
            case FailedStatus:
                ApplyFailedState(transaction, utcNow);
                break;
            case PendingStatus:
            case ProcessingStatus:
            case UnderpaidStatus:
                break;
        }

        return PaymentReconciliationErrorCode.None;
    }

    private static bool IsSupportedProviderStatus(string status)
    {
        return status is PendingStatus
            or ProcessingStatus
            or PaidStatus
            or CancelledStatus
            or UnderpaidStatus
            or ExpiredStatus
            or FailedStatus;
    }

    private static bool IsPayOsTransaction(PaymentTransaction transaction)
    {
        return string.Equals(
            transaction.PaymentProvider,
            PayOsProvider,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void UpdateProviderAudit(
        PaymentTransaction transaction,
        PayOSPaymentLinkResult providerState,
        DateTime utcNow)
    {
        var providerTransactionId =
            providerState.LatestTransactionReference
            ?? providerState.PaymentLinkId;
        if (!string.IsNullOrWhiteSpace(providerTransactionId))
        {
            transaction.ProviderTransactionId = providerTransactionId;
        }

        if (!string.IsNullOrWhiteSpace(providerState.ResponseCode))
        {
            transaction.ProviderResponseCode = providerState.ResponseCode;
        }

        if (!string.IsNullOrWhiteSpace(providerState.LatestTransactionDescription))
        {
            transaction.OrderInfo = providerState.LatestTransactionDescription;
        }

        if (!string.IsNullOrWhiteSpace(providerState.RawResponse))
        {
            transaction.RawResponse = providerState.RawResponse;
        }

        transaction.ProviderTransactionStatus = providerState.Status;
        transaction.UpdatedAt = utcNow;
    }

    private static void ApplyPaidState(
        PaymentTransaction transaction,
        DateTime utcNow)
    {
        var payment = transaction.Payment!;
        var subscription = payment.UserSubscription;
        var paidAt = payment.PaidAt ?? transaction.PaidAt ?? utcNow;

        if (payment.Status != PaymentStatus.Paid || !payment.PaidAt.HasValue)
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = paidAt;
            payment.UpdatedAt = utcNow;
        }

        transaction.Status = "Paid";
        transaction.PaidAt ??= paidAt;
        transaction.ProcessedAt ??= utcNow;

        var wasActiveLegacySubscription =
            subscription.Status == SubscriptionStatus.Active
            && subscription.EndDate.HasValue;
        if (!wasActiveLegacySubscription)
        {
            subscription.EndDate = null;
            subscription.AutoRenew = false;
            subscription.UpdatedAt = utcNow;
        }

        if (subscription.Status != SubscriptionStatus.Active
            || !subscription.StartDate.HasValue)
        {
            subscription.Status = SubscriptionStatus.Active;
            subscription.StartDate ??= paidAt;
            subscription.UpdatedAt = utcNow;
        }
    }

    private static void ApplyCancelledState(
        PaymentTransaction transaction,
        DateTime utcNow)
    {
        var payment = transaction.Payment!;
        var subscription = payment.UserSubscription;
        if (payment.Status == PaymentStatus.Paid
            || subscription.Status == SubscriptionStatus.Active)
        {
            return;
        }

        payment.Status = PaymentStatus.Cancelled;
        payment.UpdatedAt = utcNow;
        transaction.Status = "Cancelled";
        transaction.ProcessedAt ??= utcNow;
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.UpdatedAt = utcNow;
    }

    private static void ApplyFailedState(
        PaymentTransaction transaction,
        DateTime utcNow)
    {
        var payment = transaction.Payment!;
        var subscription = payment.UserSubscription;
        if (payment.Status == PaymentStatus.Paid
            || subscription.Status == SubscriptionStatus.Active)
        {
            return;
        }

        payment.Status = PaymentStatus.Failed;
        payment.UpdatedAt = utcNow;
        transaction.Status = "Failed";
        transaction.ProcessedAt ??= utcNow;
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.UpdatedAt = utcNow;
    }

    private async Task<PaymentReconciliationResult<PayOSPaymentStatusResponse>>
        RollbackReconciliationFailureAsync(
            PaymentReconciliationErrorCode error,
            string message)
    {
        await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        return PaymentReconciliationResult<PayOSPaymentStatusResponse>.Fail(error, message);
    }

    private static PaymentReconciliationErrorCode MapLookupError(
        PayOSPaymentLinkLookupError error)
    {
        return error switch
        {
            PayOSPaymentLinkLookupError.NotFound =>
                PaymentReconciliationErrorCode.ProviderNotFound,
            PayOSPaymentLinkLookupError.RateLimited =>
                PaymentReconciliationErrorCode.ProviderRateLimited,
            PayOSPaymentLinkLookupError.Unavailable =>
                PaymentReconciliationErrorCode.ProviderUnavailable,
            _ => PaymentReconciliationErrorCode.ProviderInvalidResponse,
        };
    }

    private static string BuildReconciliationFailureMessage(
        PaymentReconciliationErrorCode error)
    {
        return error switch
        {
            PaymentReconciliationErrorCode.Unauthenticated =>
                "Authentication is required.",
            PaymentReconciliationErrorCode.InvalidRequest =>
                "Invalid payment reconciliation request.",
            PaymentReconciliationErrorCode.NotFound =>
                "Payment transaction was not found.",
            PaymentReconciliationErrorCode.Forbidden =>
                "Payment transaction does not belong to the current user.",
            PaymentReconciliationErrorCode.ProviderNotFound =>
                "payOS payment link was not found.",
            PaymentReconciliationErrorCode.ProviderRateLimited =>
                "payOS rate limit was reached. Please try again later.",
            PaymentReconciliationErrorCode.ProviderUnavailable =>
                "payOS is temporarily unavailable.",
            PaymentReconciliationErrorCode.ProviderInvalidResponse =>
                "payOS returned an invalid payment state.",
            PaymentReconciliationErrorCode.OrderCodeMismatch =>
                "Payment order code does not match.",
            PaymentReconciliationErrorCode.AmountMismatch =>
                "Payment amount does not match.",
            _ => "Payment state is inconsistent.",
        };
    }

    private static bool TryGetOrderCode(
        IReadOnlyDictionary<string, string> queryParameters,
        out long orderCode)
    {
        orderCode = 0;
        var raw = GetQueryValue(queryParameters, "orderCode");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out orderCode);
    }

    private static bool TryParseOrderCode(string? value, out long orderCode)
    {
        return long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out orderCode)
            && orderCode > 0;
    }

    private static void ValidatePendingReconciliationSettings(
        PayOSPendingReconciliationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.PaymentLinkExpirationMinutes is < 5 or > 120
            || settings.MinimumAgeMinutes is < 0 or > 30
            || settings.CleanupGraceMinutes is < 0 or > 30
            || settings.BatchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "Pending payOS reconciliation settings are outside the supported range.");
        }
    }

    private static string? GetQueryValue(IReadOnlyDictionary<string, string> queryParameters, string key)
    {
        return queryParameters.TryGetValue(key, out var value) ? value : null;
    }

    private static PayOSPaymentStatusResponse BuildPaymentStatusResponse(
        PaymentTransaction transaction,
        long orderCode,
        PayOSPaymentLinkResult? providerState = null)
    {
        var payment = transaction.Payment;
        var subscription = payment?.UserSubscription ?? transaction.UserSubscription;
        var paymentStatus = payment?.Status.ToString() ?? transaction.Status ?? string.Empty;
        var subscriptionStatus = subscription?.Status.ToString() ?? string.Empty;
        var effectiveProviderStatus =
            providerState?.Status
            ?? transaction.ProviderTransactionStatus;
        var isPaid = payment?.Status == PaymentStatus.Paid;
        var isActive = subscription?.Status == SubscriptionStatus.Active;
        var isCancelled =
            payment?.Status == PaymentStatus.Cancelled
            || string.Equals(transaction.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                effectiveProviderStatus,
                CancelledStatus,
                StringComparison.OrdinalIgnoreCase);

        return new PayOSPaymentStatusResponse
        {
            OrderCode = orderCode.ToString(CultureInfo.InvariantCulture),
            PaymentId = transaction.PaymentId,
            SubscriptionId = transaction.UserSubscriptionId,
            PaymentStatus = paymentStatus,
            SubscriptionStatus = subscriptionStatus,
            ProviderStatus = effectiveProviderStatus,
            AmountPaid = providerState?.AmountPaid,
            AmountRemaining = providerState?.AmountRemaining,
            IsPaid = isPaid,
            IsActive = isActive,
            IsCancelled = isCancelled,
            Message = BuildPaymentStatusMessage(
                payment?.Status,
                subscription?.Status,
                effectiveProviderStatus),
        };
    }

    private static PagedResponse<PaymentResponse> MapToPagedResponse(
        PagedResult<Payment> paged)
    {
        return new PagedResponse<PaymentResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(MapToResponse).ToList(),
        };
    }

    private static PaymentResponse MapToResponse(Payment payment)
    {
        var latestTransaction = payment.Transactions
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        return new PaymentResponse
        {
            Id = payment.Id,
            UserId = payment.UserId,
            UserSubscriptionId = payment.UserSubscriptionId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status,
            StatusName = payment.Status.ToString(),
            PaidAt = payment.PaidAt,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt,
            PlanId = payment.UserSubscription?.PlanId,
            PlanName = payment.UserSubscription?.Plan?.PlanName,
            PaymentProvider = latestTransaction?.PaymentProvider,
            TransactionReference = latestTransaction?.TransactionReference,
        };
    }

    private static PagedResponse<PaymentResponse> CreateEmptyPagedResponse(
        int pageNumber,
        int pageSize)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : pageSize;
        normalizedPageSize = normalizedPageSize > 100 ? 100 : normalizedPageSize;

        return new PagedResponse<PaymentResponse>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = 0,
            TotalPages = 0,
            Items = Array.Empty<PaymentResponse>(),
        };
    }

    private Guid? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdValue =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("userId");

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private static string BuildPaymentStatusMessage(
        PaymentStatus? paymentStatus,
        SubscriptionStatus? subscriptionStatus,
        string? providerStatus = null)
    {
        if (paymentStatus == PaymentStatus.Paid && subscriptionStatus == SubscriptionStatus.Active)
        {
            return "Payment is paid and subscription is active.";
        }

        if (paymentStatus == PaymentStatus.Paid)
        {
            return "Payment is paid, but subscription is not active.";
        }

        if (string.Equals(providerStatus, UnderpaidStatus, StringComparison.OrdinalIgnoreCase))
        {
            return "Payment amount received by payOS is insufficient.";
        }

        if (string.Equals(providerStatus, ProcessingStatus, StringComparison.OrdinalIgnoreCase))
        {
            return "Payment is processing with payOS.";
        }

        if (string.Equals(providerStatus, PendingStatus, StringComparison.OrdinalIgnoreCase))
        {
            return "Payment is pending verification with payOS.";
        }

        if (string.Equals(providerStatus, ExpiredStatus, StringComparison.OrdinalIgnoreCase))
        {
            return "Payment link expired before payment was completed.";
        }

        if (string.Equals(providerStatus, FailedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return "Payment verification failed at payOS.";
        }

        if (string.Equals(providerStatus, CancelledStatus, StringComparison.OrdinalIgnoreCase))
        {
            return "Payment was cancelled.";
        }

        return paymentStatus switch
        {
            PaymentStatus.Pending => "Payment is pending verification with payOS.",
            PaymentStatus.Cancelled => "Payment was cancelled.",
            PaymentStatus.Failed => "Payment failed.",
            PaymentStatus.Refunded => "Payment was refunded.",
            _ => "Payment status is unavailable.",
        };
    }
}

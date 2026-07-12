using System.Globalization;
using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.DTOs.Payments.Responses;
using MedMateAI.Domain.Common;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.AspNetCore.Http;

namespace MedMateAI.Application.Service;

public sealed class PaymentService : IPaymentService
{
    private const string PaidStatus = "PAID";
    private const string CancelledStatus = "CANCELLED";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOSService _payOsService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IPayOSService payOsService,
        IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _payOsService = payOsService;
        _httpContextAccessor = httpContextAccessor;
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

        var transaction = await _unitOfWork.PaymentTransactions.GetByTransactionReferenceAsync(
            callback.OrderCode.ToString(CultureInfo.InvariantCulture),
            cancellationToken);

        if (transaction is null || transaction.Payment is null)
        {
            return false;
        }

        var amountMatches = transaction.Payment.Amount == callback.Amount;
        if (!amountMatches)
        {
            return false;
        }

        if (callback.IsPaid)
        {
            await MarkPaymentPaidAsync(
                transaction,
                callback,
                providerStatus: PaidStatus,
                responseCode: callback.Code,
                providerTransactionId: callback.Reference ?? callback.PaymentLinkId,
                orderInfo: callback.Description,
                rawResponse: callback.RawBody,
                cancellationToken);
            return true;
        }

        if (callback.IsCancelled)
        {
            await MarkPaymentCancelledAsync(
                transaction,
                callback,
                providerStatus: CancelledStatus,
                responseCode: callback.Code,
                providerTransactionId: callback.Reference ?? callback.PaymentLinkId,
                rawResponse: callback.RawBody,
                cancellationToken);
            return true;
        }

        return true;
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

    private async Task MarkPaymentPaidAsync(
        PaymentTransaction transaction,
        PayOSWebhookResult? callback,
        string providerStatus,
        string? responseCode,
        string? providerTransactionId,
        string? orderInfo,
        string? rawResponse,
        CancellationToken cancellationToken)
    {
        var payment = transaction.Payment;
        var subscription = payment?.UserSubscription;
        if (payment is null || subscription is null)
        {
            return;
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = utcNow;
        payment.UpdatedAt = utcNow;

        transaction.Status = "Paid";
        transaction.PaidAt = utcNow;
        transaction.ProcessedAt = utcNow;
        transaction.ProviderTransactionId = providerTransactionId ?? transaction.ProviderTransactionId;
        transaction.ProviderResponseCode = responseCode ?? transaction.ProviderResponseCode;
        transaction.ProviderTransactionStatus = providerStatus;
        transaction.OrderInfo = orderInfo ?? transaction.OrderInfo;
        transaction.RawResponse = rawResponse ?? transaction.RawResponse;
        transaction.UpdatedAt = utcNow;

        var durationInDays = subscription.Plan?.DurationInDays ?? 0;
        if (durationInDays <= 0)
        {
            durationInDays = 1;
        }

        subscription.Status = SubscriptionStatus.Active;
        subscription.StartDate = utcNow;
        subscription.EndDate = utcNow.AddDays(durationInDays);
        subscription.UpdatedAt = utcNow;

        _unitOfWork.Payments.Update(payment);
        _unitOfWork.PaymentTransactions.Update(transaction);
        _unitOfWork.UserSubscriptions.Update(subscription);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _ = callback;
    }

    private async Task MarkPaymentCancelledAsync(
        PaymentTransaction transaction,
        PayOSWebhookResult? callback,
        string providerStatus,
        string? responseCode,
        string? providerTransactionId,
        string? rawResponse,
        CancellationToken cancellationToken)
    {
        var payment = transaction.Payment;
        var subscription = payment?.UserSubscription;
        if (payment is null || subscription is null)
        {
            return;
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        payment.Status = PaymentStatus.Cancelled;
        payment.UpdatedAt = utcNow;

        transaction.Status = "Cancelled";
        transaction.ProcessedAt = utcNow;
        transaction.ProviderTransactionId = providerTransactionId ?? transaction.ProviderTransactionId;
        transaction.ProviderResponseCode = responseCode ?? transaction.ProviderResponseCode;
        transaction.ProviderTransactionStatus = providerStatus;
        transaction.RawResponse = rawResponse ?? transaction.RawResponse;
        transaction.UpdatedAt = utcNow;

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.UpdatedAt = utcNow;

        _unitOfWork.Payments.Update(payment);
        _unitOfWork.PaymentTransactions.Update(transaction);
        _unitOfWork.UserSubscriptions.Update(subscription);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _ = callback;
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

    private static string? GetQueryValue(IReadOnlyDictionary<string, string> queryParameters, string key)
    {
        return queryParameters.TryGetValue(key, out var value) ? value : null;
    }

    private static PayOSPaymentStatusResponse BuildPaymentStatusResponse(
        PaymentTransaction transaction,
        long orderCode)
    {
        var payment = transaction.Payment;
        var subscription = payment?.UserSubscription ?? transaction.UserSubscription;
        var paymentStatus = payment?.Status.ToString() ?? transaction.Status ?? string.Empty;
        var subscriptionStatus = subscription?.Status.ToString() ?? string.Empty;
        var isPaid = payment?.Status == PaymentStatus.Paid;
        var isActive = subscription?.Status == SubscriptionStatus.Active;
        var isCancelled =
            payment?.Status == PaymentStatus.Cancelled
            || subscription?.Status == SubscriptionStatus.Cancelled
            || string.Equals(transaction.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        return new PayOSPaymentStatusResponse
        {
            OrderCode = orderCode.ToString(CultureInfo.InvariantCulture),
            PaymentId = transaction.PaymentId,
            SubscriptionId = transaction.UserSubscriptionId,
            PaymentStatus = paymentStatus,
            SubscriptionStatus = subscriptionStatus,
            IsPaid = isPaid,
            IsActive = isActive,
            IsCancelled = isCancelled,
            Message = BuildPaymentStatusMessage(payment?.Status, subscription?.Status),
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
        SubscriptionStatus? subscriptionStatus)
    {
        if (paymentStatus == PaymentStatus.Paid && subscriptionStatus == SubscriptionStatus.Active)
        {
            return "Payment is paid and subscription is active.";
        }

        if (paymentStatus == PaymentStatus.Paid)
        {
            return "Payment is paid, but subscription is not active.";
        }

        return paymentStatus switch
        {
            PaymentStatus.Pending => "Payment is pending. Waiting for payOS webhook.",
            PaymentStatus.Cancelled => "Payment was cancelled.",
            PaymentStatus.Failed => "Payment failed.",
            PaymentStatus.Refunded => "Payment was refunded.",
            _ => "Payment status is unavailable.",
        };
    }
}

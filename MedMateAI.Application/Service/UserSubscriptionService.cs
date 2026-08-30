using System.Globalization;
using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.DTOs.UserSubscriptions.Requests;
using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Sales;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.AspNetCore.Http;

namespace MedMateAI.Application.Service;

public sealed class UserSubscriptionService : IUserSubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOSService _payOsService;
    private readonly IPaymentService _paymentService;
    private readonly ISubscriptionPlanQuotaRepository _subscriptionPlanQuotaRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISaleRedemptionService? _saleRedemptionService;

    public UserSubscriptionService(
        IUnitOfWork unitOfWork,
        IPayOSService payOsService,
        IPaymentService paymentService,
        ISubscriptionPlanQuotaRepository subscriptionPlanQuotaRepository,
        IHttpContextAccessor httpContextAccessor,
        ISaleRedemptionService? saleRedemptionService = null)
    {
        _unitOfWork = unitOfWork;
        _payOsService = payOsService;
        _paymentService = paymentService;
        _subscriptionPlanQuotaRepository = subscriptionPlanQuotaRepository;
        _httpContextAccessor = httpContextAccessor;
        _saleRedemptionService = saleRedemptionService;
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, CheckoutSubscriptionResponse? Data)> CheckoutAsync(
        CheckoutSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body is required." }, null);
        }

        var errors = new List<string>();

        if (request.PlanId == Guid.Empty)
        {
            errors.Add("PlanId is required.");
        }

        if (!Enum.IsDefined(request.ClientType))
        {
            errors.Add("ClientType is invalid.");
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            errors.Add("User is not authenticated.");
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        var plan = await _unitOfWork.SubscriptionPlans.FirstOrDefaultAsync(
            x => x.Id == request.PlanId && !x.IsDeleted,
            asNoTracking: true,
            cancellationToken: cancellationToken);

        if (plan is null)
        {
            return (false, new[] { "Subscription plan not found." }, null);
        }

        if (!plan.IsActive)
        {
            return (false, new[] { "Subscription plan is not active." }, null);
        }

        if (plan.Price <= 0)
        {
            return (false, new[] { "This plan does not require payOS payment." }, null);
        }

        var utcNow = DateTime.UtcNow;
        var orderCode = await GenerateOrderCodeAsync(cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var lockedPlan = await _subscriptionPlanQuotaRepository.GetPlanForUpdateAsync(
                plan.Id,
                cancellationToken);
            if (lockedPlan is null || !lockedPlan.IsActive)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return (false, new[] { "Subscription plan is not active." }, null);
            }

            // The optional dependency preserves source compatibility for legacy unit
            // construction. Runtime DI always supplies the sale service, so production
            // checkout uses the locked row as the authoritative plan snapshot.
            var checkoutPlan = _saleRedemptionService is null ? plan : lockedPlan;

            var planQuota = await _subscriptionPlanQuotaRepository.GetActivePlanQuotaByCodeAsync(
                checkoutPlan.Id,
                IServiceCreditService.QuotaCode,
                cancellationToken);
            if (planQuota is null || planQuota.LimitValue <= 0)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return (false, new[] { "SERVICE_CREDIT_NOT_CONFIGURED" }, null);
            }

            if (!TryConvertWholeVnd(checkoutPlan.Price, out _))
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return (false, new[] { "Plan price must be a positive whole VND amount." }, null);
            }

            var subscriptionId = Guid.NewGuid();
            var paymentId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            SaleReservationResult saleReservation;
            if (_saleRedemptionService is null)
            {
                var hasExpectedPricingSnapshot =
                    request.ExpectedOfferId.HasValue
                    || request.ExpectedEffectivePrice.HasValue
                    || request.ExpectedGrantedCredit.HasValue;
                saleReservation = hasExpectedPricingSnapshot
                    ? SaleReservationResult.Unavailable()
                    : SaleReservationResult.NoOffer();
            }
            else
            {
                saleReservation = await _saleRedemptionService.ReserveBestOfferAsync(
                    lockedPlan,
                    planQuota.LimitValue,
                    userId.GetValueOrDefault(),
                    subscriptionId,
                    paymentId,
                    request.ExpectedOfferId,
                    request.ExpectedEffectivePrice,
                    request.ExpectedGrantedCredit,
                    utcNow,
                    cancellationToken);
            }

            if (saleReservation.Outcome == SaleReservationOutcome.OfferUnavailable)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return (false, new[] { "SALE_OFFER_UNAVAILABLE" }, null);
            }

            var offer = saleReservation.Offer;
            var originalPrice = checkoutPlan.Price;
            var finalPrice = offer?.FinalPrice ?? originalPrice;
            var baseCredit = planQuota.LimitValue;
            var bonusCredit = offer?.BonusCredit ?? 0;
            var grantedCredit = offer?.GrantedCredit ?? baseCredit;
            if (!TryConvertWholeVnd(finalPrice, out var amount))
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return (false, new[] { "Final price must be a positive whole VND amount." }, null);
            }

            var subscription = new UserSubscription
            {
                Id = subscriptionId,
                UserId = userId.GetValueOrDefault(),
                PlanId = checkoutPlan.Id,
                Status = SubscriptionStatus.Pending,
                StartDate = null,
                EndDate = null,
                AutoRenew = false,
                CreatedAt = utcNow,
            };

            var payment = new Payment
            {
                Id = paymentId,
                UserId = userId.GetValueOrDefault(),
                UserSubscriptionId = subscription.Id,
                Amount = finalPrice,
                Currency = "VND",
                Status = PaymentStatus.Pending,
                CreatedAt = utcNow,
            };

            var transaction = new PaymentTransaction
            {
                Id = transactionId,
                PaymentId = payment.Id,
                UserId = userId.GetValueOrDefault(),
                UserSubscriptionId = subscription.Id,
                Amount = finalPrice,
                PaymentProvider = "payOS",
                Status = "Pending",
                TransactionReference = orderCode.ToString(CultureInfo.InvariantCulture),
                OrderInfo = $"MedMateAI {checkoutPlan.PlanName ?? "Plan"}",
                CreatedAt = utcNow,
            };

            _unitOfWork.UserSubscriptions.Add(subscription);
            _unitOfWork.Payments.Add(payment);
            _unitOfWork.PaymentTransactions.Add(transaction);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.QuotaUsages.GetOrCreateAsync(
                subscription.Id,
                planQuota.QuotaId,
                utcNow,
                cycleEnd: null,
                grantedCredit,
                utcNow,
                cancellationToken);

            PayOSCreatePaymentResult paymentLinkResult;
            try
            {
                paymentLinkResult = await _payOsService.CreatePaymentLinkAsync(
                    new PayOSCreatePaymentRequest
                    {
                        OrderCode = orderCode,
                        Amount = amount,
                        Description = $"Goi {checkoutPlan.PlanName ?? "Plan"}",
                        ReturnUrl = string.Empty,
                        CancelUrl = string.Empty,
                        UseMobileCallbacks = request.ClientType == CheckoutClientType.Mobile,
                        PaymentId = payment.Id,
                        SubscriptionId = subscription.Id,
                        UserId = userId.GetValueOrDefault(),
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                var failedAt = DateTime.UtcNow;
                payment.Status = PaymentStatus.Failed;
                payment.UpdatedAt = failedAt;

                transaction.Status = "Failed";
                transaction.ProviderTransactionStatus = "FAILED";
                transaction.ProcessedAt = failedAt;
                transaction.RawResponse = ex.Message;
                transaction.UpdatedAt = failedAt;

                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.UpdatedAt = failedAt;

                if (_saleRedemptionService is not null)
                {
                    await _saleRedemptionService.ReleaseAsync(
                        payment.Id,
                        failedAt,
                        cancellationToken);
                }

                _unitOfWork.Payments.Update(payment);
                _unitOfWork.PaymentTransactions.Update(transaction);
                _unitOfWork.UserSubscriptions.Update(subscription);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                var checkoutError = request.ClientType == CheckoutClientType.Mobile
                    && ex is InvalidOperationException
                    && ex.Message.StartsWith("MobilePayment:", StringComparison.Ordinal)
                        ? ex.Message
                        : "Create payOS payment link failed.";
                return (false, new[] { checkoutError }, null);
            }

            transaction.ProviderTransactionId = paymentLinkResult.PaymentLinkId;
            transaction.ProviderTransactionStatus = paymentLinkResult.Status;
            transaction.RawResponse = paymentLinkResult.RawResponse;
            transaction.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.PaymentTransactions.Update(transaction);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return (true, Array.Empty<string>(), new CheckoutSubscriptionResponse
            {
                SubscriptionId = subscription.Id,
                PaymentId = payment.Id,
                TransactionId = transaction.Id,
                OrderCode = orderCode.ToString(CultureInfo.InvariantCulture),
                PaymentUrl = paymentLinkResult.CheckoutUrl,
                PaymentProvider = "payOS",
                OriginalPrice = originalPrice,
                FinalPrice = finalPrice,
                DiscountAmount = originalPrice - finalPrice,
                BaseCredit = baseCredit,
                BonusCredit = bonusCredit,
                GrantedCredit = grantedCredit,
                AppliedSaleCampaignId = offer?.CampaignId,
                AppliedSaleCampaignPlanId = offer?.OfferId,
                SaleCampaignName = offer?.CampaignName,
                SaleBadgeText = offer?.BadgeText,
            });
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            return (false, new[] { "Checkout failed." }, null);
        }
    }

    public async Task<IReadOnlyList<UserSubscriptionResponse>> GetMySubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Array.Empty<UserSubscriptionResponse>();
        }

        var subscriptions = await _unitOfWork.UserSubscriptions.GetByUserWithPlanAsync(
            userId.Value,
            cancellationToken);

        return subscriptions.Select(MapToResponse).ToList();
    }

    public async Task<UserSubscriptionResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var subscription = await _unitOfWork.UserSubscriptions.GetByIdWithPlanAsync(id, cancellationToken);
        if (subscription is null || subscription.IsDeleted)
        {
            return null;
        }

        return MapToResponse(subscription);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, UserSubscriptionResponse? Data)> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid subscription id." }, null);
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return (false, false, new[] { "User is not authenticated." }, null);
        }

        var subscription = await _unitOfWork.UserSubscriptions.GetByIdWithPlanAsync(id, cancellationToken);
        if (subscription is null || subscription.IsDeleted)
        {
            return (false, true, Array.Empty<string>(), null);
        }

        if (subscription.UserId != userId.Value)
        {
            return (false, true, Array.Empty<string>(), null);
        }

        if (subscription.Status != SubscriptionStatus.Pending)
        {
            return (
                false,
                false,
                new[] { "Only pending subscriptions can be cancelled." },
                null);
        }

        var cancellation = await _paymentService.CancelPendingPayOSCheckoutAsync(
            subscription.Id,
            userId.Value,
            cancellationToken);
        if (!cancellation.Success)
        {
            return (
                false,
                false,
                new[] { cancellation.Message ?? "Cancel subscription failed." },
                null);
        }

        var updated = await _unitOfWork.UserSubscriptions.GetByIdWithPlanAsync(id, cancellationToken);
        return updated is null
            ? (false, true, Array.Empty<string>(), null)
            : (true, false, Array.Empty<string>(), MapToResponse(updated));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, PagedResponse<UserSubscriptionResponse>? Data)>
        GetAdminSubscriptionsAsync(
            int pageNumber,
            int pageSize,
            string? status,
            bool currentOnly,
            CancellationToken cancellationToken = default)
    {
        if (!TryParseStatus(status, out SubscriptionStatus? parsedStatus))
        {
            return (false, new[] { "Invalid user subscription status." }, null);
        }

        var paged = await _unitOfWork.UserSubscriptions.GetAdminPagedAsync(
            pageNumber,
            pageSize,
            parsedStatus,
            currentOnly,
            DateTime.UtcNow,
            cancellationToken);

        return (
            true,
            Array.Empty<string>(),
            PagedResponse<UserSubscriptionResponse>.From(paged, MapToResponse));
    }

    private async Task<long> GenerateOrderCodeAsync(CancellationToken cancellationToken)
    {
        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        while (await _unitOfWork.PaymentTransactions.GetByTransactionReferenceAsync(
                   orderCode.ToString(),
                   cancellationToken) is not null)
        {
            await Task.Delay(1, cancellationToken);
            orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        return orderCode;
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

    private static UserSubscriptionResponse MapToResponse(UserSubscription subscription)
    {
        var plan = subscription.Plan;

        return new UserSubscriptionResponse
        {
            Id = subscription.Id,
            UserId = subscription.UserId,
            PlanId = subscription.PlanId,
            PlanName = plan?.PlanName,
            Price = plan?.Price ?? 0,
            DurationInDays = plan?.DurationInDays ?? 0,
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            Status = subscription.Status,
            StatusName = subscription.Status.ToString(),
            AutoRenew = subscription.AutoRenew,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt,
        };
    }

    private static bool TryConvertWholeVnd(decimal amount, out int value)
    {
        value = 0;
        if (amount <= 0
            || amount != decimal.Truncate(amount)
            || amount > int.MaxValue)
        {
            return false;
        }

        value = decimal.ToInt32(amount);
        return true;
    }

    private static bool TryParseStatus<TStatus>(string? value, out TStatus? status)
        where TStatus : struct, Enum
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Enum.TryParse<TStatus>(value.Trim(), ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }
}

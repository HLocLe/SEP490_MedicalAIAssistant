using System.Globalization;
using System.Security.Claims;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.DTOs.UserSubscriptions.Requests;
using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.AspNetCore.Http;

namespace MedMateAI.Application.Service;

public sealed class UserSubscriptionService : IUserSubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOSService _payOsService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserSubscriptionService(
        IUnitOfWork unitOfWork,
        IPayOSService payOsService,
        IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _payOsService = payOsService;
        _httpContextAccessor = httpContextAccessor;
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
        var activeSubscription = await _unitOfWork.UserSubscriptions.GetCurrentActiveByUserAsync(
            userId!.Value,
            utcNow,
            cancellationToken);

        if (activeSubscription is not null)
        {
            return (false, new[] { "You already have an active subscription." }, null);
        }

        var amount = decimal.ToInt32(plan.Price);
        var orderCode = await GenerateOrderCodeAsync(cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var subscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Pending,
                StartDate = null,
                EndDate = null,
                AutoRenew = request.AutoRenew,
                CreatedAt = utcNow,
            };

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                UserSubscriptionId = subscription.Id,
                Amount = plan.Price,
                Currency = "VND",
                Status = PaymentStatus.Pending,
                CreatedAt = utcNow,
            };

            var transaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                UserId = userId.Value,
                UserSubscriptionId = subscription.Id,
                Amount = plan.Price,
                PaymentProvider = "payOS",
                Status = "Pending",
                TransactionReference = orderCode.ToString(CultureInfo.InvariantCulture),
                OrderInfo = $"MedMateAI {plan.PlanName ?? "Plan"}",
                CreatedAt = utcNow,
            };

            _unitOfWork.UserSubscriptions.Add(subscription);
            _unitOfWork.Payments.Add(payment);
            _unitOfWork.PaymentTransactions.Add(transaction);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            PayOSCreatePaymentResult paymentLinkResult;
            try
            {
                paymentLinkResult = await _payOsService.CreatePaymentLinkAsync(
                    new PayOSCreatePaymentRequest
                    {
                        OrderCode = orderCode,
                        Amount = amount,
                        Description = $"Goi {plan.PlanName ?? "Plan"}",
                        ReturnUrl = string.Empty,
                        CancelUrl = string.Empty,
                        PaymentId = payment.Id,
                        SubscriptionId = subscription.Id,
                        UserId = userId.Value,
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

                _unitOfWork.Payments.Update(payment);
                _unitOfWork.PaymentTransactions.Update(transaction);
                _unitOfWork.UserSubscriptions.Update(subscription);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return (false, new[] { "Create payOS payment link failed." }, null);
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
            });
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
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

        if (subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Pending)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.AutoRenew = false;
            subscription.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.UserSubscriptions.Update(subscription);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var updated = await _unitOfWork.UserSubscriptions.GetByIdWithPlanAsync(id, cancellationToken);
        return updated is null
            ? (false, true, Array.Empty<string>(), null)
            : (true, false, Array.Empty<string>(), MapToResponse(updated));
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
}

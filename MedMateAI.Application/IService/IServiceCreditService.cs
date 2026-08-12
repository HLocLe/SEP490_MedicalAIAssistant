using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.ServiceCredits;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.IService;

public interface IServiceCreditService
{
    public const string QuotaCode = "SERVICE_CREDIT";

    Task<ServiceCreditOperationResult<SubscriptionUsageResponse>> GetBalanceAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<ServiceCreditOperationResult<UserSubscriptionUsage>> ReserveAsync(
        Guid userId,
        string referenceType,
        Guid referenceId,
        Guid? actorUserId,
        string idempotencyKey,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<QuotaMutationStatus> MutateAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        SubscriptionQuotaActionType actionType,
        string referenceType,
        Guid referenceId,
        Guid? actorUserId,
        string idempotencyKey,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<QuotaMutationStatus> GrantAsync(
        Guid userSubscriptionId,
        Guid paymentId,
        Guid actorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.ServiceCredits;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanQuotaService : IRecoveryPlanQuotaService
{
    public const string QuotaCode = IServiceCreditService.QuotaCode;
    public const string ReferenceType = "RecoveryPlanRequest";

    private const string ReserveReason = "Recovery plan service credit reserved.";
    private const string ReleaseReason = "Recovery plan service credit released.";
    private const string ConsumeReason = "Recovery plan service credit consumed.";
    private const string RestoreReason = "Recovery plan service credit restored.";

    private readonly IServiceCreditService _serviceCreditService;

    public RecoveryPlanQuotaService(IServiceCreditService serviceCreditService)
    {
        _serviceCreditService = serviceCreditService;
    }

    public async Task<RecoveryPlanOperationResult<UserSubscriptionUsage>> ReserveUsageAsync(
        Guid userId,
        Guid requestId,
        Guid actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var result = await _serviceCreditService.ReserveAsync(
            userId,
            ReferenceType,
            requestId,
            actorUserId,
            idempotencyKey,
            ReserveReason,
            utcNow,
            cancellationToken);

        return result.Success && result.Data is not null
            ? RecoveryPlanOperationResult<UserSubscriptionUsage>.Ok(
                result.Data,
                result.IsReplay)
            : RecoveryPlanOperationResult<UserSubscriptionUsage>.Fail(
                MapError(result.Error));
    }

    public Task<QuotaMutationStatus> ReleaseAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return MutateAsync(
            usageId,
            userSubscriptionId,
            quotaId,
            SubscriptionQuotaActionType.Release,
            requestId,
            actorUserId,
            idempotencyKey,
            ReleaseReason,
            utcNow,
            cancellationToken);
    }

    public Task<QuotaMutationStatus> ConsumeAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return MutateAsync(
            usageId,
            userSubscriptionId,
            quotaId,
            SubscriptionQuotaActionType.Consume,
            requestId,
            actorUserId,
            idempotencyKey,
            ConsumeReason,
            utcNow,
            cancellationToken);
    }

    public Task<QuotaMutationStatus> RestoreAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return MutateAsync(
            usageId,
            userSubscriptionId,
            quotaId,
            SubscriptionQuotaActionType.Restore,
            requestId,
            actorUserId,
            idempotencyKey,
            RestoreReason,
            utcNow,
            cancellationToken);
    }

    private Task<QuotaMutationStatus> MutateAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        SubscriptionQuotaActionType actionType,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return _serviceCreditService.MutateAsync(
            usageId,
            userSubscriptionId,
            quotaId,
            actionType,
            ReferenceType,
            requestId,
            actorUserId,
            idempotencyKey,
            reason,
            utcNow,
            cancellationToken);
    }

    private static RecoveryPlanErrorCode MapError(ServiceCreditErrorCode error) =>
        error switch
        {
            ServiceCreditErrorCode.NoCreditPackage =>
                RecoveryPlanErrorCode.NoCreditPackage,
            ServiceCreditErrorCode.ServiceCreditExhausted =>
                RecoveryPlanErrorCode.ServiceCreditExhausted,
            ServiceCreditErrorCode.ServiceCreditNotConfigured =>
                RecoveryPlanErrorCode.ServiceCreditNotConfigured,
            _ => RecoveryPlanErrorCode.QuotaMutationFailed
        };
}

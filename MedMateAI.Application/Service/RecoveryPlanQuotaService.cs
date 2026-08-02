using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanQuotaService : IRecoveryPlanQuotaService
{
    public const string QuotaCode = "RECOVERY_PLAN_REQUEST";
    public const string ReferenceType = "RECOVERY_PLAN_REQUEST";

    private const string ReserveReason = "Recovery plan request quota reserved.";
    private const string ReleaseReason = "Recovery plan request quota released.";
    private const string ConsumeReason = "Recovery plan request quota consumed.";
    private const string RestoreReason = "Recovery plan request quota restored.";

    private readonly IUnitOfWork _unitOfWork;

    public RecoveryPlanQuotaService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<RecoveryPlanOperationResult<UserSubscriptionUsage>> ResolveUsageAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var subscription = await _unitOfWork.UserSubscriptions.GetCurrentActiveWithPlanQuotasAsync(
            userId,
            utcNow,
            cancellationToken);

        if (subscription is null)
        {
            return RecoveryPlanOperationResult<UserSubscriptionUsage>.Fail(RecoveryPlanErrorCode.NoActiveSubscription);
        }

        var planQuota = subscription.Plan.SubscriptionPlanQuotas.FirstOrDefault(IsRecoveryPlanQuota);
        if (planQuota is null)
        {
            return RecoveryPlanOperationResult<UserSubscriptionUsage>.Fail(RecoveryPlanErrorCode.RecoveryPlanQuotaNotConfigured);
        }

        if (planQuota.LimitValue <= 0)
        {
            return RecoveryPlanOperationResult<UserSubscriptionUsage>.Fail(RecoveryPlanErrorCode.RecoveryPlanQuotaExhausted);
        }

        var usage = await _unitOfWork.QuotaUsages.GetOrCreateAsync(
            subscription.Id,
            planQuota.QuotaId,
            subscription.StartDate!.Value,
            subscription.EndDate!.Value,
            planQuota.LimitValue,
            utcNow,
            cancellationToken);

        return RecoveryPlanOperationResult<UserSubscriptionUsage>.Ok(usage);
    }

    public async Task<RecoveryPlanOperationResult<IReadOnlyList<SubscriptionUsageResponse>>> GetCurrentUsageAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var subscription = await _unitOfWork.UserSubscriptions.GetCurrentActiveWithPlanQuotasAsync(
            userId,
            utcNow,
            cancellationToken);

        if (subscription is null)
        {
            return RecoveryPlanOperationResult<IReadOnlyList<SubscriptionUsageResponse>>.Fail(RecoveryPlanErrorCode.NoActiveSubscription);
        }

        var usages = await _unitOfWork.QuotaUsages.GetBySubscriptionAsync(
            subscription.Id,
            cancellationToken);

        var cycleStart = subscription.StartDate!.Value;
        var cycleEnd = subscription.EndDate!.Value;

        var items = subscription.Plan.SubscriptionPlanQuotas
            .Where(planQuota =>
                !planQuota.IsDeleted
                && planQuota.IsActive
                && !planQuota.Quota.IsDeleted
                && planQuota.Quota.IsActive)
            .Select(planQuota => MapUsage(planQuota, usages, cycleStart, cycleEnd))
            .ToList();

        return RecoveryPlanOperationResult<IReadOnlyList<SubscriptionUsageResponse>>.Ok(items);
    }

    public Task<QuotaMutationStatus> ReserveAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        Guid requestId,
        Guid actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return MutateAsync(
            () => _unitOfWork.QuotaUsages.ReserveAsync(usageId, utcNow, cancellationToken),
            SubscriptionQuotaActionType.Reserve,
            requestId,
            actorUserId,
            idempotencyKey,
            ReserveReason,
            utcNow,
            cancellationToken);
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
            () => _unitOfWork.QuotaUsages.ReleaseAsync(usageId, utcNow, cancellationToken),
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
            () => _unitOfWork.QuotaUsages.ConsumeAsync(usageId, utcNow, cancellationToken),
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
            () => _unitOfWork.QuotaUsages.RestoreAsync(usageId, utcNow, cancellationToken),
            SubscriptionQuotaActionType.Restore,
            requestId,
            actorUserId,
            idempotencyKey,
            RestoreReason,
            utcNow,
            cancellationToken);
    }

    private async Task<QuotaMutationStatus> MutateAsync(
        Func<Task<QuotaMutationResult?>> mutation,
        SubscriptionQuotaActionType actionType,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var existingLog = await _unitOfWork.QuotaUsages.GetLogByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);

        if (existingLog is not null)
        {
            return QuotaMutationStatus.Duplicate;
        }

        var mutationResult = await mutation();
        if (mutationResult is null)
        {
            return QuotaMutationStatus.Rejected;
        }

        var log = CreateMutationLog(
            mutationResult,
            actionType,
            requestId,
            actorUserId,
            idempotencyKey,
            reason,
            utcNow);

        var logInserted = await _unitOfWork.QuotaUsages.TryInsertLogAsync(log, cancellationToken);
        if (logInserted)
        {
            return QuotaMutationStatus.Applied;
        }

        return QuotaMutationStatus.Duplicate;
    }

    private static UserSubscriptionLog CreateMutationLog(
        QuotaMutationResult mutationResult,
        SubscriptionQuotaActionType actionType,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        string reason,
        DateTime utcNow)
    {
        return new UserSubscriptionLog
        {
            Id = Guid.NewGuid(),
            UserSubscriptionId = mutationResult.UserSubscriptionId,
            UserSubscriptionUsageId = mutationResult.UsageId,
            QuotaId = mutationResult.QuotaId,
            ActionType = actionType,
            Quantity = 1,
            UsedCountBefore = mutationResult.UsedCountBefore,
            UsedCountAfter = mutationResult.UsedCountAfter,
            ReservedCountBefore = mutationResult.ReservedCountBefore,
            ReservedCountAfter = mutationResult.ReservedCountAfter,
            ReferenceType = ReferenceType,
            ReferenceId = requestId,
            Reason = reason,
            IdempotencyKey = idempotencyKey,
            PerformedByUserId = actorUserId,
            CreatedAt = utcNow,
        };
    }

    private static SubscriptionUsageResponse MapUsage(
        SubscriptionPlanQuota planQuota,
        IReadOnlyList<UserSubscriptionUsage> usages,
        DateTime cycleStart,
        DateTime cycleEnd)
    {
        var usage = usages.FirstOrDefault(currentUsage =>
            currentUsage.QuotaId == planQuota.QuotaId
            && currentUsage.CycleStart == cycleStart
            && currentUsage.CycleEnd == cycleEnd);

        return new SubscriptionUsageResponse
        {
            QuotaCode = planQuota.Quota.Code,
            QuotaName = planQuota.Quota.Name,
            LimitValue = usage?.LimitValue ?? planQuota.LimitValue,
            UsedCount = usage?.UsedCount ?? 0,
            ReservedCount = usage?.ReservedCount ?? 0,
            CycleStart = cycleStart,
            CycleEnd = cycleEnd,
            ResetPeriod = planQuota.ResetPeriod,
        };
    }

    private static bool IsRecoveryPlanQuota(SubscriptionPlanQuota planQuota)
    {
        return !planQuota.IsDeleted
            && planQuota.IsActive
            && planQuota.ResetPeriod == QuotaResetPeriod.SubscriptionCycle
            && !planQuota.Quota.IsDeleted
            && planQuota.Quota.IsActive
            && string.Equals(planQuota.Quota.Code, QuotaCode, StringComparison.Ordinal);
    }
}

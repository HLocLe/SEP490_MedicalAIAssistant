using MedMateAI.Application.DTOs.RecoveryPlanRequests;
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
    private readonly IUnitOfWork _unitOfWork;
    public RecoveryPlanQuotaService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<RecoveryPlanOperationResult<UserSubscriptionUsage>> ResolveUsageAsync(
        Guid userId, DateTime now, CancellationToken token)
    {
        var subscription = await _unitOfWork.UserSubscriptions.GetCurrentActiveWithPlanQuotasAsync(userId, now, token);
        if (subscription is null)
            return RecoveryPlanOperationResult<UserSubscriptionUsage>.Fail(RecoveryPlanErrorCode.NoActiveSubscription);
        var planQuota = subscription.Plan.SubscriptionPlanQuotas.FirstOrDefault(IsRecoveryPlanQuota);
        if (planQuota is null)
            return RecoveryPlanOperationResult<UserSubscriptionUsage>.Fail(RecoveryPlanErrorCode.RecoveryPlanQuotaNotConfigured);
        if (planQuota.LimitValue <= 0)
            return RecoveryPlanOperationResult<UserSubscriptionUsage>.Fail(RecoveryPlanErrorCode.RecoveryPlanQuotaExhausted);
        var usage = await _unitOfWork.QuotaUsages.GetOrCreateAsync(
            subscription.Id, planQuota.QuotaId, subscription.StartDate!.Value,
            subscription.EndDate!.Value, planQuota.LimitValue, now, token);
        return RecoveryPlanOperationResult<UserSubscriptionUsage>.Ok(usage);
    }

    public async Task<RecoveryPlanOperationResult<IReadOnlyList<SubscriptionUsageResponse>>> GetCurrentUsageAsync(Guid userId, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var subscription = await _unitOfWork.UserSubscriptions.GetCurrentActiveWithPlanQuotasAsync(userId, now, token);
        if (subscription is null)
            return RecoveryPlanOperationResult<IReadOnlyList<SubscriptionUsageResponse>>.Fail(RecoveryPlanErrorCode.NoActiveSubscription);
        var usages = await _unitOfWork.QuotaUsages.GetBySubscriptionAsync(subscription.Id, token);
        var cycleStart = subscription.StartDate!.Value;
        var cycleEnd = subscription.EndDate!.Value;
        var items = subscription.Plan.SubscriptionPlanQuotas
            .Where(x => !x.IsDeleted && x.IsActive && !x.Quota.IsDeleted && x.Quota.IsActive)
            .Select(x =>
            {
                var usage = usages.FirstOrDefault(u => u.QuotaId == x.QuotaId
                    && u.CycleStart == cycleStart && u.CycleEnd == cycleEnd);
                return new SubscriptionUsageResponse
                {
                    QuotaCode = x.Quota.Code, QuotaName = x.Quota.Name,
                    LimitValue = usage?.LimitValue ?? x.LimitValue,
                    UsedCount = usage?.UsedCount ?? 0, ReservedCount = usage?.ReservedCount ?? 0,
                    CycleStart = cycleStart, CycleEnd = cycleEnd,
                    ResetPeriod = x.ResetPeriod
                };
            }).ToList();
        return RecoveryPlanOperationResult<IReadOnlyList<SubscriptionUsageResponse>>.Ok(items);
    }

    public Task<bool> ReserveAsync(Guid usageId, Guid subscriptionId, Guid quotaId, Guid requestId, Guid actorId, string key, DateTime now, CancellationToken token) =>
        MutateAsync(() => _unitOfWork.QuotaUsages.ReserveAsync(usageId, now, token), SubscriptionQuotaActionType.Reserve,
            subscriptionId, quotaId, requestId, actorId, key, "Recovery plan request quota reserved.", now, token);
    public Task<bool> ReleaseAsync(Guid usageId, Guid subscriptionId, Guid quotaId, Guid requestId, Guid? actorId, string key, DateTime now, CancellationToken token) =>
        MutateAsync(() => _unitOfWork.QuotaUsages.ReleaseAsync(usageId, now, token), SubscriptionQuotaActionType.Release,
            subscriptionId, quotaId, requestId, actorId, key, "Recovery plan request quota released.", now, token);
    public Task<bool> ConsumeAsync(Guid usageId, Guid subscriptionId, Guid quotaId, Guid requestId, Guid? actorId, string key, DateTime now, CancellationToken token) =>
        MutateAsync(() => _unitOfWork.QuotaUsages.ConsumeAsync(usageId, now, token), SubscriptionQuotaActionType.Consume,
            subscriptionId, quotaId, requestId, actorId, key, "Recovery plan request quota consumed.", now, token);
    public Task<bool> RestoreAsync(Guid usageId, Guid subscriptionId, Guid quotaId, Guid requestId, Guid? actorId, string key, DateTime now, CancellationToken token) =>
        MutateAsync(() => _unitOfWork.QuotaUsages.RestoreAsync(usageId, now, token), SubscriptionQuotaActionType.Restore,
            subscriptionId, quotaId, requestId, actorId, key, "Recovery plan request quota restored.", now, token);

    private async Task<bool> MutateAsync(
        Func<Task<QuotaMutationResult?>> mutation, SubscriptionQuotaActionType action,
        Guid subscriptionId, Guid quotaId, Guid requestId, Guid? actorId,
        string key, string reason, DateTime now, CancellationToken token)
    {
        if (await _unitOfWork.QuotaUsages.GetLogByIdempotencyKeyAsync(key, token) is not null) return true;
        var result = await mutation();
        if (result is null) return false;
        return await _unitOfWork.QuotaUsages.TryInsertLogAsync(new UserSubscriptionLog
        {
            Id = Guid.NewGuid(), UserSubscriptionId = subscriptionId,
            UserSubscriptionUsageId = result.UsageId, QuotaId = quotaId,
            ActionType = action, Quantity = 1,
            UsedCountBefore = result.UsedCountBefore, UsedCountAfter = result.UsedCountAfter,
            ReservedCountBefore = result.ReservedCountBefore, ReservedCountAfter = result.ReservedCountAfter,
            ReferenceType = ReferenceType, ReferenceId = requestId, Reason = reason,
            IdempotencyKey = key, PerformedByUserId = actorId, CreatedAt = now
        }, token);
    }

    private static bool IsRecoveryPlanQuota(SubscriptionPlanQuota x) =>
        !x.IsDeleted && x.IsActive && x.ResetPeriod == QuotaResetPeriod.SubscriptionCycle
        && !x.Quota.IsDeleted && x.Quota.IsActive
        && string.Equals(x.Quota.Code, QuotaCode, StringComparison.Ordinal);
}

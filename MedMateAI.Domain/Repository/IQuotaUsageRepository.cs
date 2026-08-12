using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IQuotaUsageRepository
{
    Task AcquireIdempotencyLockAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<UserSubscriptionUsage> GetOrCreateAsync(
        Guid subscriptionId, Guid quotaId, DateTime cycleStart, DateTime? cycleEnd,
        int limitValue, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscriptionUsage>> GetEligibleByUserAsync(
        Guid userId,
        string quotaCode,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task<QuotaMutationResult?> ReserveAsync(Guid usageId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<QuotaMutationResult?> ReleaseAsync(Guid usageId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<QuotaMutationResult?> ConsumeAsync(Guid usageId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<QuotaMutationResult?> RestoreAsync(Guid usageId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<UserSubscriptionLog?> GetLogByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<bool> TryInsertLogAsync(UserSubscriptionLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscriptionUsage>> GetBySubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<UserSubscriptionUsage?> GetByIdAsync(Guid usageId, CancellationToken cancellationToken = default);
    Task<UserSubscriptionUsage?> GetByIdForQuotaAsync(
        Guid usageId,
        Guid userSubscriptionId,
        string quotaCode,
        CancellationToken cancellationToken = default);
    Task<UserSubscriptionUsage?> GetBySubscriptionForQuotaAsync(
        Guid userSubscriptionId,
        string quotaCode,
        CancellationToken cancellationToken = default);
}

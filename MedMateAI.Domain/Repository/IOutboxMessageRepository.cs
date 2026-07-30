using MedMateAI.Domain.Common;

namespace MedMateAI.Domain.Repository;

public interface IOutboxMessageRepository
{
    Task<IReadOnlyList<OutboxProcessingItem>> ClaimBatchAsync(
        DateTime utcNow,
        int batchSize,
        TimeSpan processingLease,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlanNotificationData?> GetRecoveryPlanNotificationDataAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<bool> RenewLeaseAsync(
        Guid outboxMessageId,
        int attemptCount,
        DateTime leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> MarkProcessedAsync(
        Guid outboxMessageId,
        int attemptCount,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> ScheduleRetryAsync(
        Guid outboxMessageId,
        int attemptCount,
        DateTime retryAtUtc,
        string lastError,
        CancellationToken cancellationToken = default);

    Task<bool> MarkFailedAsync(
        Guid outboxMessageId,
        int attemptCount,
        string lastError,
        CancellationToken cancellationToken = default);
}

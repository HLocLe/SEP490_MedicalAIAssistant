using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface INotificationRepository
{
    Task<bool> TryInsertAsync(
        Notification notification,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationProcessingItem>> ClaimBatchAsync(
        DateTime utcNow,
        int batchSize,
        TimeSpan processingLease,
        CancellationToken cancellationToken = default);

    Task<NotificationRecipientData?> GetRecipientAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlanNotificationReferenceData?> GetRecoveryPlanReferenceAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<MedicationReminderNotificationData?> GetMedicationReminderReferenceAsync(
        Guid reminderTimeId,
        CancellationToken cancellationToken = default);

    Task<bool> RenewLeaseAsync(
        Guid notificationId,
        int attemptCount,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> MarkSentAsync(
        Guid notificationId,
        int attemptCount,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> ScheduleRetryAsync(
        Guid notificationId,
        int attemptCount,
        DateTime retryAtUtc,
        DateTime utcNow,
        string lastError,
        CancellationToken cancellationToken = default);

    Task<bool> MarkFailedAsync(
        Guid notificationId,
        int attemptCount,
        DateTime utcNow,
        string lastError,
        CancellationToken cancellationToken = default);

    Task<bool> MarkCancelledAsync(
        Guid notificationId,
        int attemptCount,
        DateTime utcNow,
        string lastError,
        CancellationToken cancellationToken = default);
}

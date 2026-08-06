using MedMateAI.Application.Common;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryInsertAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.DedupeKey))
        {
            throw new ArgumentException(
                "A dedupe key is required for a durable notification.",
                nameof(notification));
        }

        // The filtered unique index makes replay and concurrent inserts idempotent.
        var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Notification"
                ("NotificationId", "UserId", "ReminderId", "Title", "Message",
                 "Channel", "Status", "SentAt", "NotificationType", "ReferenceType",
                 "ReferenceId", "ScheduledAt", "AttemptCount", "LastError", "DedupeKey",
                 "CreatedAt", "UpdatedAt", "IsDeleted", "DeletedAt")
            VALUES
                ({notification.Id}, {notification.UserId}, {notification.ReminderId},
                 {notification.Title}, {notification.Message}, {notification.Channel},
                 {notification.Status}, {notification.SentAt}, {notification.NotificationType},
                 {notification.ReferenceType}, {notification.ReferenceId},
                 {notification.ScheduledAt}, {notification.AttemptCount},
                 {notification.LastError}, {notification.DedupeKey},
                 {notification.CreatedAt}, {notification.UpdatedAt},
                 {notification.IsDeleted}, {notification.DeletedAt})
            ON CONFLICT ("DedupeKey")
                WHERE "DedupeKey" IS NOT NULL
            DO NOTHING;
            """,
            cancellationToken);

        return affectedRows == 1;
    }

    public async Task<IReadOnlyList<NotificationProcessingItem>> ClaimBatchAsync(
        DateTime utcNow,
        int batchSize,
        TimeSpan processingLease,
        CancellationToken cancellationToken = default)
    {
        var staleBeforeUtc = utcNow.Subtract(processingLease);
        var emailChannel = NotificationChannels.Email;
        var pendingStatus = NotificationStatuses.Pending;
        var processingStatus = NotificationStatuses.Processing;
        var readyType = NotificationTypes.RecoveryPlanReady;
        var completedType = NotificationTypes.RecoveryPlanCompleted;
        var cancelledType = NotificationTypes.RecoveryPlanCancelled;
        var medicationReminderType = NotificationTypes.MedicationReminder;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // SKIP LOCKED gives every worker instance a different due-email batch.
            var notifications = await _context.Notifications
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "Notification"
                    WHERE "IsDeleted" = FALSE
                      AND "Channel" = {emailChannel}
                      AND "NotificationType" IN (
                          {readyType},
                          {completedType},
                          {cancelledType},
                          {medicationReminderType})
                      AND (
                          ("Status" = {pendingStatus}
                           AND ("ScheduledAt" IS NULL OR "ScheduledAt" <= {utcNow}))
                          OR
                          ("Status" = {processingStatus}
                           AND ("UpdatedAt" IS NULL OR "UpdatedAt" <= {staleBeforeUtc}))
                      )
                    ORDER BY
                        ("ScheduledAt" IS NOT NULL),
                        "CreatedAt",
                        "NotificationId"
                    FOR UPDATE SKIP LOCKED
                    LIMIT {batchSize}
                    """)
                .AsTracking()
                .ToListAsync(cancellationToken);

            foreach (var notification in notifications)
            {
                notification.Status = NotificationStatuses.Processing;
                notification.AttemptCount++;
                notification.UpdatedAt = utcNow;
                notification.LastError = null;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var claimedItems = notifications
                .Select(notification => new NotificationProcessingItem(
                    notification.Id,
                    notification.UserId,
                    notification.NotificationType,
                    notification.ReferenceType,
                    notification.ReferenceId,
                    notification.ScheduledAt,
                    notification.AttemptCount))
                .ToList();

            _context.ChangeTracker.Clear();
            return claimedItems;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<NotificationRecipientData?> GetRecipientAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new NotificationRecipientData(
                user.Id,
                user.Email,
                user.TimeZoneId,
                !user.IsDeleted && user.Status == UserStatus.Confirmed))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<RecoveryPlanNotificationReferenceData?> GetRecoveryPlanReferenceAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        return _context.RecoveryPlans
            .AsNoTracking()
            .Where(plan => plan.Id == planId && !plan.IsDeleted)
            .Select(plan => new RecoveryPlanNotificationReferenceData(
                plan.Id,
                plan.UserId,
                plan.Status,
                plan.PlanName,
                plan.CancelledAt,
                plan.CancellationReasonCode,
                plan.CancellationReason))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<MedicationReminderNotificationData?> GetMedicationReminderReferenceAsync(
        Guid reminderTimeId,
        CancellationToken cancellationToken = default)
    {
        return (
            from reminderTime in _context.UserMedicationReminderTimes.AsNoTracking()
            join medication in _context.UserMedications.AsNoTracking()
                on reminderTime.UserMedicationId equals medication.Id
            where reminderTime.Id == reminderTimeId
                  && !reminderTime.IsDeleted
                  && !medication.IsDeleted
                  && medication.SourceType == UserMedicationSourceType.PatientReported
                  && medication.TreatmentJourneyId == null
            select new MedicationReminderNotificationData(
                reminderTime.Id,
                medication.UserId,
                medication.MedicineName,
                medication.DosageInstruction,
                medication.StartDate,
                medication.EndDate,
                reminderTime.TimeOfDay,
                reminderTime.IsActive,
                medication.IsReminderEnabled))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> RenewLeaseAsync(
        Guid notificationId,
        int attemptCount,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await ProcessingAttempt(notificationId, attemptCount)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    notification => notification.UpdatedAt,
                    utcNow),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkSentAsync(
        Guid notificationId,
        int attemptCount,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await ProcessingAttempt(notificationId, attemptCount)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.Status, NotificationStatuses.Sent)
                    .SetProperty(notification => notification.SentAt, utcNow)
                    .SetProperty(notification => notification.UpdatedAt, utcNow)
                    .SetProperty(notification => notification.LastError, (string?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> ScheduleRetryAsync(
        Guid notificationId,
        int attemptCount,
        DateTime retryAtUtc,
        DateTime utcNow,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await ProcessingAttempt(notificationId, attemptCount)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.Status, NotificationStatuses.Pending)
                    .SetProperty(notification => notification.ScheduledAt, retryAtUtc)
                    .SetProperty(notification => notification.UpdatedAt, utcNow)
                    .SetProperty(notification => notification.SentAt, (DateTime?)null)
                    .SetProperty(notification => notification.LastError, lastError),
                cancellationToken);

        return affectedRows == 1;
    }

    public Task<bool> MarkFailedAsync(
        Guid notificationId,
        int attemptCount,
        DateTime utcNow,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        return MarkTerminalAsync(
            notificationId,
            attemptCount,
            NotificationStatuses.Failed,
            utcNow,
            lastError,
            cancellationToken);
    }

    public Task<bool> MarkCancelledAsync(
        Guid notificationId,
        int attemptCount,
        DateTime utcNow,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        return MarkTerminalAsync(
            notificationId,
            attemptCount,
            NotificationStatuses.Cancelled,
            utcNow,
            lastError,
            cancellationToken);
    }

    private async Task<bool> MarkTerminalAsync(
        Guid notificationId,
        int attemptCount,
        string status,
        DateTime utcNow,
        string lastError,
        CancellationToken cancellationToken)
    {
        var affectedRows = await ProcessingAttempt(notificationId, attemptCount)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.Status, status)
                    .SetProperty(notification => notification.UpdatedAt, utcNow)
                    .SetProperty(notification => notification.SentAt, (DateTime?)null)
                    .SetProperty(notification => notification.LastError, lastError),
                cancellationToken);

        return affectedRows == 1;
    }

    private IQueryable<Notification> ProcessingAttempt(
        Guid notificationId,
        int attemptCount)
    {
        return _context.Notifications.Where(notification =>
            notification.Id == notificationId
            && !notification.IsDeleted
            && notification.Status == NotificationStatuses.Processing
            && notification.AttemptCount == attemptCount);
    }
}

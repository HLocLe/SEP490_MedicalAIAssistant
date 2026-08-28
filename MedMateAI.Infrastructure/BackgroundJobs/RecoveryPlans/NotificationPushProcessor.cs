using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using MedMateAI.Infrastructure.Push.Expo.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class NotificationPushProcessor : INotificationPushProcessor
{
    private const int LeaseSafetyMarginSeconds = 5;
    private const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";
    private const string RecipientIneligibleError = "Recipient is no longer eligible.";
    private const string DeviceIneligibleError = "Push device is no longer eligible.";
    private const string ReferenceIneligibleError =
        "Notification reference is no longer eligible.";
    private const string UnsupportedTypeError = "Unsupported notification type.";
    private const string DeliveryFailureError = "Push delivery failed.";

    private readonly INotificationRepository _notificationRepository;
    private readonly IUserPushDeviceRepository _pushDeviceRepository;
    private readonly IPushNotificationGateway _gateway;
    private readonly RecoveryPlanJobOptions _jobOptions;
    private readonly ExpoPushOptions _pushOptions;
    private readonly TimeSpan _deliveryTimeout;
    private readonly ILogger<NotificationPushProcessor> _logger;

    public NotificationPushProcessor(
        INotificationRepository notificationRepository,
        IUserPushDeviceRepository pushDeviceRepository,
        IPushNotificationGateway gateway,
        IOptions<RecoveryPlanJobOptions> jobOptions,
        IOptions<ExpoPushOptions> pushOptions,
        ILogger<NotificationPushProcessor> logger)
    {
        _notificationRepository = notificationRepository;
        _pushDeviceRepository = pushDeviceRepository;
        _gateway = gateway;
        _jobOptions = jobOptions.Value;
        _pushOptions = pushOptions.Value;
        _logger = logger;

        var timeoutSeconds = Math.Min(
            _pushOptions.RequestTimeoutSeconds,
            Math.Max(1, _jobOptions.ProcessingLeaseSeconds - LeaseSafetyMarginSeconds));
        _deliveryTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public async Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (!_pushOptions.Enabled)
        {
            return;
        }

        var notifications = await _notificationRepository.ClaimPushBatchAsync(
            utcNow,
            _jobOptions.BatchSize,
            TimeSpan.FromSeconds(_jobOptions.ProcessingLeaseSeconds),
            cancellationToken);

        foreach (var notification in notifications)
        {
            try
            {
                await ProcessNotificationAsync(notification, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Push notification {NotificationId} could not update its state after {FailureType}.",
                    notification.Id,
                    exception.GetType().Name);
            }
        }
    }

    private async Task ProcessNotificationAsync(
        PushNotificationProcessingItem notification,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await RenewLeaseAsync(notification, "delivery preparation", cancellationToken))
            {
                return;
            }

            var recipient = await _notificationRepository.GetRecipientAsync(
                notification.UserId,
                cancellationToken);
            if (recipient is null || !recipient.IsEligible)
            {
                await CancelAsync(notification, RecipientIneligibleError, cancellationToken);
                return;
            }

            if (!notification.IsDeviceEligible)
            {
                await CancelAsync(notification, DeviceIneligibleError, cancellationToken);
                return;
            }

            var preparation = await PrepareDeliveryAsync(
                notification,
                recipient,
                DateTime.UtcNow,
                cancellationToken);
            if (!preparation.Ready)
            {
                await CancelAsync(
                    notification,
                    preparation.Error ?? ReferenceIneligibleError,
                    cancellationToken);
                return;
            }

            if (!await RenewLeaseAsync(notification, "provider delivery", cancellationToken))
            {
                return;
            }

            var currentDevice = await _pushDeviceRepository.GetActiveAsync(
                notification.PushDeviceId,
                notification.UserId,
                cancellationToken);
            if (currentDevice is null
                || string.IsNullOrWhiteSpace(currentDevice.ExpoPushToken))
            {
                await CancelAsync(notification, DeviceIneligibleError, cancellationToken);
                return;
            }

            var data = new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id.ToString("D"),
                ["notificationType"] = notification.NotificationType,
                ["referenceType"] = notification.ReferenceType ?? string.Empty,
                ["referenceId"] = notification.ReferenceId?.ToString("D") ?? string.Empty
            };
            var message = new PushNotificationMessage(
                currentDevice.ExpoPushToken,
                preparation.Title!,
                preparation.Body!,
                data,
                preparation.TimeToLiveSeconds);

            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_deliveryTimeout);
            var result = await _gateway.SendAsync(message, timeoutSource.Token);
            await HandleSendResultAsync(
                notification,
                currentDevice.TokenVersion,
                result,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Push notification {NotificationId} delivery failed with {FailureType}.",
                notification.Id,
                exception.GetType().Name);
            await RetryOrFailAsync(notification, DeliveryFailureError, cancellationToken);
        }
    }

    private async Task<PushPreparation> PrepareDeliveryAsync(
        PushNotificationProcessingItem notification,
        NotificationRecipientData recipient,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return notification.NotificationType switch
        {
            NotificationTypes.RecoveryPlanReady =>
                await PrepareRecoveryPlanAsync(
                    notification,
                    RecoveryPlanStatus.ReadyToStart,
                    PushNotificationContent.RecoveryPlanReadyTitle,
                    PushNotificationContent.RecoveryPlanReadyMessage,
                    requireCancellationMetadata: false,
                    cancellationToken),
            NotificationTypes.RecoveryPlanCompleted =>
                await PrepareRecoveryPlanAsync(
                    notification,
                    RecoveryPlanStatus.Completed,
                    PushNotificationContent.RecoveryPlanCompletedTitle,
                    PushNotificationContent.RecoveryPlanCompletedMessage,
                    requireCancellationMetadata: false,
                    cancellationToken),
            NotificationTypes.RecoveryPlanCancelled =>
                await PrepareRecoveryPlanAsync(
                    notification,
                    RecoveryPlanStatus.Cancelled,
                    PushNotificationContent.RecoveryPlanCancelledTitle,
                    PushNotificationContent.RecoveryPlanCancelledMessage,
                    requireCancellationMetadata: true,
                    cancellationToken),
            NotificationTypes.MedicationReminder =>
                await PrepareMedicationReminderAsync(
                    notification,
                    recipient,
                    utcNow,
                    cancellationToken),
            _ => PushPreparation.Cancelled(UnsupportedTypeError)
        };
    }

    private async Task<PushPreparation> PrepareRecoveryPlanAsync(
        PushNotificationProcessingItem notification,
        RecoveryPlanStatus requiredStatus,
        string title,
        string body,
        bool requireCancellationMetadata,
        CancellationToken cancellationToken)
    {
        if (notification.ReferenceType != NotificationReferenceTypes.RecoveryPlan
            || !notification.ReferenceId.HasValue)
        {
            return PushPreparation.Cancelled(ReferenceIneligibleError);
        }

        var plan = await _notificationRepository.GetRecoveryPlanReferenceAsync(
            notification.ReferenceId.Value,
            cancellationToken);
        if (plan is null
            || plan.UserId != notification.UserId
            || plan.Status != requiredStatus
            || (requireCancellationMetadata
                && (!plan.CancelledAt.HasValue
                    || !RecoveryPlanCancellationReasons.TryNormalize(
                        plan.CancellationReasonCode,
                        plan.CancellationReason,
                        out _,
                        out _))))
        {
            return PushPreparation.Cancelled(ReferenceIneligibleError);
        }

        return PushPreparation.Deliver(title, body, null);
    }

    private async Task<PushPreparation> PrepareMedicationReminderAsync(
        PushNotificationProcessingItem notification,
        NotificationRecipientData recipient,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (notification.ReferenceType
                != NotificationReferenceTypes.UserMedicationReminderTime
            || !notification.ReferenceId.HasValue
            || !notification.ScheduledAt.HasValue)
        {
            return PushPreparation.Cancelled(ReferenceIneligibleError);
        }

        var reminder = await _notificationRepository.GetMedicationReminderReferenceAsync(
            notification.ReferenceId.Value,
            cancellationToken);
        if (reminder is null
            || reminder.UserId != notification.UserId
            || !reminder.IsReminderActive
            || !reminder.IsMedicationReminderEnabled
            || string.IsNullOrWhiteSpace(reminder.MedicineName))
        {
            return PushPreparation.Cancelled(ReferenceIneligibleError);
        }

        var scheduledAtUtc = AsUtc(notification.ScheduledAt.Value);
        var expiresAtUtc = scheduledAtUtc.AddMinutes(
            _jobOptions.MedicationMaxLatenessMinutes);
        var remaining = expiresAtUtc - utcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return PushPreparation.Cancelled(ReferenceIneligibleError);
        }

        var timeZone = ResolveTimeZone(recipient.TimeZoneId, notification.Id);
        var localScheduledAt = TimeZoneInfo.ConvertTimeFromUtc(
            scheduledAtUtc,
            timeZone);
        var localDate = DateOnly.FromDateTime(localScheduledAt);
        if ((reminder.StartDate.HasValue && localDate < reminder.StartDate.Value)
            || (reminder.EndDate.HasValue && localDate > reminder.EndDate.Value))
        {
            return PushPreparation.Cancelled(ReferenceIneligibleError);
        }

        var localTime = TimeOnly.FromDateTime(localScheduledAt);
        if (localTime.Hour != reminder.TimeOfDay.Hour
            || localTime.Minute != reminder.TimeOfDay.Minute)
        {
            return PushPreparation.Cancelled(ReferenceIneligibleError);
        }

        var ttlSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
        return PushPreparation.Deliver(
            PushNotificationContent.MedicationReminderTitle,
            PushNotificationContent.MedicationReminderMessage,
            ttlSeconds);
    }

    private async Task HandleSendResultAsync(
        PushNotificationProcessingItem notification,
        int providerPushTokenVersion,
        PushSendResult result,
        CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case PushSendOutcome.Accepted
                when !string.IsNullOrWhiteSpace(result.ProviderMessageId):
            {
                var utcNow = DateTime.UtcNow;
                var updated = await _notificationRepository.MarkSubmittedAsync(
                    notification.Id,
                    notification.AttemptCount,
                    result.ProviderMessageId,
                    providerPushTokenVersion,
                    utcNow,
                    utcNow.AddMinutes(_pushOptions.ReceiptDelayMinutes),
                    utcNow,
                    cancellationToken);
                LogLeaseLossIfNeeded(updated, notification, "mark submitted");
                break;
            }
            case PushSendOutcome.InvalidDevice:
                await _pushDeviceRepository.DeactivateIfTokenVersionMatchesAsync(
                    notification.PushDeviceId,
                    notification.UserId,
                    providerPushTokenVersion,
                    DateTime.UtcNow,
                    cancellationToken);
                await CancelAsync(notification, DeviceIneligibleError, cancellationToken);
                break;
            case PushSendOutcome.RetryableFailure:
                await RetryOrFailAsync(
                    notification,
                    result.ErrorCode ?? DeliveryFailureError,
                    cancellationToken);
                break;
            default:
                await FailAsync(
                    notification,
                    result.ErrorCode ?? DeliveryFailureError,
                    cancellationToken);
                break;
        }
    }

    private async Task RetryOrFailAsync(
        PushNotificationProcessingItem notification,
        string error,
        CancellationToken cancellationToken)
    {
        if (notification.AttemptCount >= _jobOptions.MaxAttempts)
        {
            await FailAsync(notification, error, cancellationToken);
            return;
        }

        var utcNow = DateTime.UtcNow;
        var retryAtUtc = RecoveryPlanJobRetrySchedule.GetRetryAtUtc(
            notification.Id,
            notification.AttemptCount,
            utcNow,
            _jobOptions);
        var updated = await _notificationRepository.ScheduleRetryAsync(
            notification.Id,
            notification.AttemptCount,
            retryAtUtc,
            utcNow,
            error,
            cancellationToken);
        LogLeaseLossIfNeeded(updated, notification, "schedule retry");
    }

    private async Task FailAsync(
        PushNotificationProcessingItem notification,
        string error,
        CancellationToken cancellationToken)
    {
        var updated = await _notificationRepository.MarkFailedAsync(
            notification.Id,
            notification.AttemptCount,
            DateTime.UtcNow,
            error,
            cancellationToken);
        LogLeaseLossIfNeeded(updated, notification, "mark failed");
    }

    private async Task CancelAsync(
        PushNotificationProcessingItem notification,
        string error,
        CancellationToken cancellationToken)
    {
        var updated = await _notificationRepository.MarkCancelledAsync(
            notification.Id,
            notification.AttemptCount,
            DateTime.UtcNow,
            error,
            cancellationToken);
        LogLeaseLossIfNeeded(updated, notification, "mark cancelled");
    }

    private async Task<bool> RenewLeaseAsync(
        PushNotificationProcessingItem notification,
        string operation,
        CancellationToken cancellationToken)
    {
        var updated = await _notificationRepository.RenewLeaseAsync(
            notification.Id,
            notification.AttemptCount,
            DateTime.UtcNow,
            cancellationToken);
        LogLeaseLossIfNeeded(updated, notification, operation);
        return updated;
    }

    private void LogLeaseLossIfNeeded(
        bool updated,
        PushNotificationProcessingItem notification,
        string operation)
    {
        if (!updated)
        {
            _logger.LogWarning(
                "Push notification {NotificationId} lost its processing lease before {Operation}.",
                notification.Id,
                operation);
        }
    }

    private TimeZoneInfo ResolveTimeZone(string? timeZoneId, Guid notificationId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                LogTimeZoneFallback(notificationId);
            }
            catch (InvalidTimeZoneException)
            {
                LogTimeZoneFallback(notificationId);
            }
        }
        else
        {
            LogTimeZoneFallback(notificationId);
        }

        return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
    }

    private void LogTimeZoneFallback(Guid notificationId)
    {
        _logger.LogWarning(
            "Push medication notification {NotificationId} has no valid user timezone; using the default timezone.",
            notificationId);
    }

    private static DateTime AsUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    private sealed record PushPreparation(
        bool Ready,
        string? Title,
        string? Body,
        int? TimeToLiveSeconds,
        string? Error)
    {
        public static PushPreparation Deliver(
            string title,
            string body,
            int? ttlSeconds)
        {
            return new PushPreparation(true, title, body, ttlSeconds, null);
        }

        public static PushPreparation Cancelled(string error)
        {
            return new PushPreparation(false, null, null, null, error);
        }
    }
}

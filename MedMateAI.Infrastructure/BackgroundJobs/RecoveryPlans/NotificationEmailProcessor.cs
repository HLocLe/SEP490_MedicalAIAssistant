using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class NotificationEmailProcessor : INotificationEmailProcessor
{
    private const int DefaultDeliveryTimeoutSeconds = 30;
    private const int LeaseSafetyMarginSeconds = 5;
    private const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";
    private const string RecipientIneligibleError = "Recipient is no longer eligible.";
    private const string ReferenceIneligibleError =
        "Notification reference is no longer eligible.";
    private const string UnsupportedTypeError = "Unsupported notification type.";
    private const string DeliveryFailureError = "Email delivery failed.";
    private const string SaleIneligibleError =
        "Sale campaign is no longer eligible.";
    private const string SaleUndeliverableError =
        "Sale campaign notification is no longer deliverable.";

    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationEmailRenderer _renderer;
    private readonly IEmailSender _emailSender;
    private readonly ISaleCampaignAnnouncementContextService _saleContextService;
    private readonly ISaleCampaignNotificationContentBuilder _saleContentBuilder;
    private readonly RecoveryPlanJobOptions _options;
    private readonly TimeSpan _deliveryTimeout;
    private readonly ILogger<NotificationEmailProcessor> _logger;

    public NotificationEmailProcessor(
        INotificationRepository notificationRepository,
        INotificationEmailRenderer renderer,
        IEmailSender emailSender,
        ISaleCampaignAnnouncementContextService saleContextService,
        ISaleCampaignNotificationContentBuilder saleContentBuilder,
        IOptions<RecoveryPlanJobOptions> options,
        ILogger<NotificationEmailProcessor> logger)
    {
        _notificationRepository = notificationRepository;
        _renderer = renderer;
        _emailSender = emailSender;
        _saleContextService = saleContextService;
        _saleContentBuilder = saleContentBuilder;
        _options = options.Value;
        _logger = logger;

        var timeoutSeconds = Math.Min(
            DefaultDeliveryTimeoutSeconds,
            Math.Max(1, _options.ProcessingLeaseSeconds - LeaseSafetyMarginSeconds));
        _deliveryTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public async Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationRepository.ClaimBatchAsync(
            utcNow,
            _options.BatchSize,
            TimeSpan.FromSeconds(_options.ProcessingLeaseSeconds),
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
                    "Notification {NotificationId} could not update its processing state after {FailureType}.",
                    notification.Id,
                    exception.GetType().Name);
            }
        }
    }

    private async Task ProcessNotificationAsync(
        NotificationProcessingItem notification,
        CancellationToken cancellationToken)
    {
        try
        {
            var leaseRenewed = await _notificationRepository.RenewLeaseAsync(
                notification.Id,
                notification.AttemptCount,
                DateTime.UtcNow,
                cancellationToken);
            if (!leaseRenewed)
            {
                LogLeaseLossIfNeeded(
                    false,
                    notification,
                    "delivery preparation");
                return;
            }

            var recipient = await _notificationRepository.GetRecipientAsync(
                notification.UserId,
                cancellationToken);

            if (recipient is null
                || !recipient.IsEligible
                || string.IsNullOrWhiteSpace(recipient.Email))
            {
                await CancelAsync(
                    notification,
                    RecipientIneligibleError,
                    cancellationToken);
                return;
            }

            var preparation = await PrepareDeliveryAsync(
                notification,
                recipient,
                DateTime.UtcNow,
                cancellationToken);

            if (preparation.Status == DeliveryPreparationStatus.Cancelled)
            {
                await CancelAsync(
                    notification,
                    preparation.Error!,
                    cancellationToken);
                return;
            }

            if (preparation.Status == DeliveryPreparationStatus.Failed)
            {
                await FailAsync(
                    notification,
                    preparation.Error!,
                    cancellationToken);
                return;
            }

            leaseRenewed = await _notificationRepository.RenewLeaseAsync(
                notification.Id,
                notification.AttemptCount,
                DateTime.UtcNow,
                cancellationToken);
            if (!leaseRenewed)
            {
                LogLeaseLossIfNeeded(
                    false,
                    notification,
                    "email delivery");
                return;
            }

            await SendEmailAsync(
                recipient.Email,
                preparation.Content!,
                cancellationToken);

            var sentAtUtc = DateTime.UtcNow;
            var updated = await _notificationRepository.MarkSentAsync(
                notification.Id,
                notification.AttemptCount,
                sentAtUtc,
                cancellationToken);
            LogLeaseLossIfNeeded(updated, notification, "mark sent");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await HandleRetryableFailureAsync(
                notification,
                exception,
                cancellationToken);
        }
    }

    private async Task<DeliveryPreparation> PrepareDeliveryAsync(
        NotificationProcessingItem notification,
        NotificationRecipientData recipient,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        switch (notification.NotificationType)
        {
            case NotificationTypes.RecoveryPlanReady:
                return await PrepareRecoveryPlanEmailAsync(
                    notification,
                    RecoveryPlanStatus.ReadyToStart,
                    _renderer.RenderRecoveryPlanReady,
                    cancellationToken);

            case NotificationTypes.RecoveryPlanCompleted:
                return await PrepareRecoveryPlanEmailAsync(
                    notification,
                    RecoveryPlanStatus.Completed,
                    _renderer.RenderRecoveryPlanCompleted,
                    cancellationToken);

            case NotificationTypes.RecoveryPlanCancelled:
                return await PrepareRecoveryPlanCancelledEmailAsync(
                    notification,
                    cancellationToken);

            case NotificationTypes.MedicationReminder:
                return await PrepareMedicationReminderEmailAsync(
                    notification,
                    recipient,
                    utcNow,
                    cancellationToken);

            case NotificationTypes.SaleCampaignAnnouncement:
                return await PrepareSaleCampaignEmailAsync(
                    notification,
                    utcNow,
                    cancellationToken);

            default:
                return DeliveryPreparation.Failed(UnsupportedTypeError);
        }
    }

    private async Task<DeliveryPreparation> PrepareSaleCampaignEmailAsync(
        NotificationProcessingItem notification,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (notification.ReferenceType != NotificationReferenceTypes.SaleCampaign
            || !notification.ReferenceId.HasValue)
        {
            return DeliveryPreparation.Cancelled(SaleIneligibleError);
        }

        var context = await _saleContextService.GetEligibleContextAsync(
            notification.UserId,
            notification.ReferenceId.Value,
            utcNow,
            cancellationToken);
        if (context is null)
        {
            return DeliveryPreparation.Cancelled(SaleIneligibleError);
        }

        if (string.IsNullOrWhiteSpace(context.Email))
        {
            return DeliveryPreparation.Cancelled(SaleUndeliverableError);
        }

        var content = _saleContentBuilder.Build(
            context,
            NotificationChannels.Email);
        return DeliveryPreparation.Ready(
            _renderer.RenderSaleCampaignAnnouncement(context, content));
    }

    private async Task<DeliveryPreparation> PrepareRecoveryPlanEmailAsync(
        NotificationProcessingItem notification,
        RecoveryPlanStatus requiredStatus,
        Func<NotificationEmailContent> render,
        CancellationToken cancellationToken)
    {
        if (notification.ReferenceType != NotificationReferenceTypes.RecoveryPlan
            || !notification.ReferenceId.HasValue)
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        var plan = await _notificationRepository.GetRecoveryPlanReferenceAsync(
            notification.ReferenceId.Value,
            cancellationToken);

        if (plan is null
            || plan.UserId != notification.UserId
            || plan.Status != requiredStatus)
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        return DeliveryPreparation.Ready(render());
    }

    private async Task<DeliveryPreparation> PrepareRecoveryPlanCancelledEmailAsync(
        NotificationProcessingItem notification,
        CancellationToken cancellationToken)
    {
        if (notification.ReferenceType != NotificationReferenceTypes.RecoveryPlan
            || !notification.ReferenceId.HasValue)
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        var plan = await _notificationRepository.GetRecoveryPlanReferenceAsync(
            notification.ReferenceId.Value,
            cancellationToken);

        if (plan is null
            || plan.UserId != notification.UserId
            || plan.Status != RecoveryPlanStatus.Cancelled
            || !plan.CancelledAt.HasValue
            || !RecoveryPlanCancellationReasons.TryNormalize(
                plan.CancellationReasonCode,
                plan.CancellationReason,
                out var cancellationReasonCode,
                out var cancellationReason))
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        return DeliveryPreparation.Ready(
            _renderer.RenderRecoveryPlanCancelled(
                plan.PlanName,
                cancellationReasonCode,
                cancellationReason));
    }

    private async Task<DeliveryPreparation> PrepareMedicationReminderEmailAsync(
        NotificationProcessingItem notification,
        NotificationRecipientData recipient,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (notification.ReferenceType
                != NotificationReferenceTypes.UserMedicationReminderTime
            || !notification.ReferenceId.HasValue
            || !notification.ScheduledAt.HasValue)
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
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
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        var scheduledAtUtc = AsUtc(notification.ScheduledAt.Value);
        var maximumLateness =
            TimeSpan.FromMinutes(_options.MedicationMaxLatenessMinutes);
        if (utcNow - scheduledAtUtc > maximumLateness)
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        var timeZone = ResolveTimeZone(
            recipient.TimeZoneId,
            notification.Id);

        var localScheduledAt = TimeZoneInfo.ConvertTimeFromUtc(
            scheduledAtUtc,
            timeZone);
        var localDate = DateOnly.FromDateTime(localScheduledAt);
        if (reminder.StartDate.HasValue && localDate < reminder.StartDate.Value)
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        if (reminder.EndDate.HasValue && localDate > reminder.EndDate.Value)
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        var localTime = TimeOnly.FromDateTime(localScheduledAt);
        if (localTime.Hour != reminder.TimeOfDay.Hour
            || localTime.Minute != reminder.TimeOfDay.Minute)
        {
            return DeliveryPreparation.Cancelled(ReferenceIneligibleError);
        }

        return DeliveryPreparation.Ready(
            _renderer.RenderMedicationReminder(
                reminder.MedicineName,
                reminder.DosageInstruction));
    }

    private async Task SendEmailAsync(
        string recipientEmail,
        NotificationEmailContent content,
        CancellationToken cancellationToken)
    {
        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_deliveryTimeout);

        await _emailSender.SendAsync(
            recipientEmail,
            content.Subject,
            content.HtmlBody,
            timeoutSource.Token);
    }

    private async Task HandleRetryableFailureAsync(
        NotificationProcessingItem notification,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Notification {NotificationId} delivery failed with {FailureType}.",
            notification.Id,
            exception.GetType().Name);

        if (notification.AttemptCount >= _options.MaxAttempts)
        {
            await FailAsync(
                notification,
                DeliveryFailureError,
                cancellationToken);
            return;
        }

        var utcNow = DateTime.UtcNow;
        var retryAtUtc = RecoveryPlanJobRetrySchedule.GetRetryAtUtc(
            notification.Id,
            notification.AttemptCount,
            utcNow,
            _options);
        var updated = await _notificationRepository.ScheduleRetryAsync(
            notification.Id,
            notification.AttemptCount,
            retryAtUtc,
            utcNow,
            DeliveryFailureError,
            cancellationToken);
        LogLeaseLossIfNeeded(updated, notification, "schedule retry");
    }

    private async Task FailAsync(
        NotificationProcessingItem notification,
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
        NotificationProcessingItem notification,
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

    private void LogLeaseLossIfNeeded(
        bool updated,
        NotificationProcessingItem notification,
        string operation)
    {
        if (updated)
        {
            return;
        }

        _logger.LogWarning(
            "Notification {NotificationId} lost its processing lease before {Operation}.",
            notification.Id,
            operation);
    }

    private static DateTime AsUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }

    private TimeZoneInfo ResolveTimeZone(
        string? timeZoneId,
        Guid notificationId)
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
            "Medication notification {NotificationId} has no valid user timezone; using the default timezone.",
            notificationId);
    }

    private enum DeliveryPreparationStatus
    {
        Ready,
        Cancelled,
        Failed
    }

    private sealed record DeliveryPreparation(
        DeliveryPreparationStatus Status,
        NotificationEmailContent? Content,
        string? Error)
    {
        public static DeliveryPreparation Ready(NotificationEmailContent content)
        {
            return new DeliveryPreparation(
                DeliveryPreparationStatus.Ready,
                content,
                null);
        }

        public static DeliveryPreparation Cancelled(string error)
        {
            return new DeliveryPreparation(
                DeliveryPreparationStatus.Cancelled,
                null,
                error);
        }

        public static DeliveryPreparation Failed(string error)
        {
            return new DeliveryPreparation(
                DeliveryPreparationStatus.Failed,
                null,
                error);
        }
    }
}

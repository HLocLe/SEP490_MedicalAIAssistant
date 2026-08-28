using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Repository;
using MedMateAI.Infrastructure.Push.Expo.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class NotificationPushReceiptProcessor :
    INotificationPushReceiptProcessor
{
    private const int LeaseSafetyMarginSeconds = 5;
    private const string ReceiptUnavailableError = "Push receipt is unavailable.";
    private const string ReceiptRejectedError = "Push receipt was rejected.";
    private const string DeviceIneligibleError = "Push device is no longer eligible.";

    private readonly INotificationRepository _notificationRepository;
    private readonly IUserPushDeviceRepository _pushDeviceRepository;
    private readonly IPushNotificationGateway _gateway;
    private readonly RecoveryPlanJobOptions _jobOptions;
    private readonly ExpoPushOptions _pushOptions;
    private readonly TimeSpan _receiptTimeout;
    private readonly ILogger<NotificationPushReceiptProcessor> _logger;

    public NotificationPushReceiptProcessor(
        INotificationRepository notificationRepository,
        IUserPushDeviceRepository pushDeviceRepository,
        IPushNotificationGateway gateway,
        IOptions<RecoveryPlanJobOptions> jobOptions,
        IOptions<ExpoPushOptions> pushOptions,
        ILogger<NotificationPushReceiptProcessor> logger)
    {
        _notificationRepository = notificationRepository;
        _pushDeviceRepository = pushDeviceRepository;
        _gateway = gateway;
        _jobOptions = jobOptions.Value;
        _pushOptions = pushOptions.Value;
        _logger = logger;

        var receiptTimeoutSeconds = Math.Min(
            _pushOptions.RequestTimeoutSeconds,
            Math.Max(
                1,
                _jobOptions.ProcessingLeaseSeconds - LeaseSafetyMarginSeconds));
        _receiptTimeout = TimeSpan.FromSeconds(receiptTimeoutSeconds);
    }

    public async Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (!_pushOptions.Enabled)
        {
            return;
        }

        var notifications = await _notificationRepository
            .ClaimPushReceiptBatchAsync(
                utcNow,
                _pushOptions.ReceiptBatchSize,
                TimeSpan.FromSeconds(_jobOptions.ProcessingLeaseSeconds),
                cancellationToken);
        if (notifications.Count == 0)
        {
            return;
        }

        PushReceiptBatchResult batchResult;
        try
        {
            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_receiptTimeout);
            batchResult = await _gateway.GetReceiptsAsync(
                notifications
                    .Select(notification => notification.ProviderMessageId)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Push receipt batch failed with {FailureType}.",
                exception.GetType().Name);
            batchResult = new PushReceiptBatchResult(
                false,
                true,
                new Dictionary<string, PushReceiptResult>(),
                ReceiptUnavailableError);
        }

        foreach (var notification in notifications)
        {
            try
            {
                await ProcessReceiptAsync(
                    notification,
                    batchResult,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Push receipt for notification {NotificationId} could not update its state after {FailureType}.",
                    notification.Id,
                    exception.GetType().Name);
            }
        }
    }

    private async Task ProcessReceiptAsync(
        PushReceiptProcessingItem notification,
        PushReceiptBatchResult batchResult,
        CancellationToken cancellationToken)
    {
        if (!batchResult.Success)
        {
            if (batchResult.IsRetryableFailure)
            {
                await RetryReceiptOrFailAsync(
                    notification,
                    batchResult.ErrorCode ?? ReceiptUnavailableError,
                    cancellationToken);
            }
            else
            {
                await FailAsync(
                    notification,
                    batchResult.ErrorCode ?? ReceiptRejectedError,
                    cancellationToken);
            }

            return;
        }

        if (!batchResult.Receipts.TryGetValue(
                notification.ProviderMessageId,
                out var receipt))
        {
            await RetryReceiptOrFailAsync(
                notification,
                ReceiptUnavailableError,
                cancellationToken);
            return;
        }

        switch (receipt.Outcome)
        {
            case PushReceiptOutcome.Delivered:
                await _notificationRepository.MarkReceiptSentAsync(
                    notification.Id,
                    notification.ReceiptAttemptCount,
                    DateTime.UtcNow,
                    cancellationToken);
                break;
            case PushReceiptOutcome.DeviceNotRegistered:
                if (notification.ProviderPushTokenVersion.HasValue)
                {
                    await _pushDeviceRepository
                        .DeactivateIfTokenVersionMatchesAsync(
                            notification.PushDeviceId,
                            notification.UserId,
                            notification.ProviderPushTokenVersion.Value,
                            DateTime.UtcNow,
                            cancellationToken);
                }
                else
                {
                    _logger.LogWarning(
                        "Push notification {NotificationId} for device {PushDeviceId} has no submitted token version; the device was not deactivated.",
                        notification.Id,
                        notification.PushDeviceId);
                }

                await _notificationRepository.MarkReceiptCancelledAsync(
                    notification.Id,
                    notification.ReceiptAttemptCount,
                    DateTime.UtcNow,
                    DeviceIneligibleError,
                    cancellationToken);
                break;
            case PushReceiptOutcome.MessageRateExceeded:
                await RequeueForSendOrFailAsync(
                    notification,
                    receipt.ErrorCode ?? ReceiptUnavailableError,
                    cancellationToken);
                break;
            default:
                await FailAsync(
                    notification,
                    receipt.ErrorCode ?? ReceiptRejectedError,
                    cancellationToken);
                break;
        }
    }

    private async Task RetryReceiptOrFailAsync(
        PushReceiptProcessingItem notification,
        string error,
        CancellationToken cancellationToken)
    {
        if (notification.ReceiptAttemptCount >= _pushOptions.ReceiptMaxAttempts)
        {
            await FailAsync(notification, error, cancellationToken);
            return;
        }

        var utcNow = DateTime.UtcNow;
        await _notificationRepository.ScheduleReceiptRetryAsync(
            notification.Id,
            notification.ReceiptAttemptCount,
            utcNow.AddMinutes(_pushOptions.ReceiptRetryMinutes),
            utcNow,
            error,
            cancellationToken);
    }

    private async Task RequeueForSendOrFailAsync(
        PushReceiptProcessingItem notification,
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
        await _notificationRepository.SchedulePushResendAsync(
            notification.Id,
            notification.ReceiptAttemptCount,
            retryAtUtc,
            utcNow,
            error,
            cancellationToken);
    }

    private Task<bool> FailAsync(
        PushReceiptProcessingItem notification,
        string error,
        CancellationToken cancellationToken)
    {
        return _notificationRepository.MarkReceiptFailedAsync(
            notification.Id,
            notification.ReceiptAttemptCount,
            DateTime.UtcNow,
            error,
            cancellationToken);
    }
}

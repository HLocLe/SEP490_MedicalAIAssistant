using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class RecoveryPlanOutboxProcessor : IOutboxMessageProcessor
{
    private const string UnsupportedEventError = "Unsupported outbox event type.";
    private const string InvalidAggregateError = "Invalid outbox aggregate type.";
    private const string ProcessingFailureError = "Outbox processing failed.";

    private readonly IOutboxMessageRepository _outboxRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly RecoveryPlanJobOptions _options;
    private readonly ILogger<RecoveryPlanOutboxProcessor> _logger;

    public RecoveryPlanOutboxProcessor(
        IOutboxMessageRepository outboxRepository,
        INotificationRepository notificationRepository,
        IOptions<RecoveryPlanJobOptions> options,
        ILogger<RecoveryPlanOutboxProcessor> logger)
    {
        _outboxRepository = outboxRepository;
        _notificationRepository = notificationRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var messages = await _outboxRepository.ClaimBatchAsync(
            utcNow,
            _options.BatchSize,
            TimeSpan.FromSeconds(_options.ProcessingLeaseSeconds),
            cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Outbox event {EventType} for aggregate {AggregateId} could not update its processing state after {FailureType}.",
                    message.EventType,
                    message.AggregateId,
                    exception.GetType().Name);
            }
        }
    }

    private async Task ProcessMessageAsync(
        OutboxProcessingItem message,
        CancellationToken cancellationToken)
    {
        try
        {
            var leaseExpiresAtUtc = DateTime.UtcNow.AddSeconds(
                _options.ProcessingLeaseSeconds);
            var leaseRenewed = await _outboxRepository.RenewLeaseAsync(
                message.Id,
                message.AttemptCount,
                leaseExpiresAtUtc,
                cancellationToken);
            if (!leaseRenewed)
            {
                LogLeaseLossIfNeeded(false, message);
                return;
            }

            var outcome = await HandleMessageAsync(message, cancellationToken);
            var utcNow = DateTime.UtcNow;

            if (outcome.IsTerminalFailure)
            {
                await MarkFailedAsync(message, outcome.Error!, cancellationToken);
                return;
            }

            var updated = await _outboxRepository.MarkProcessedAsync(
                message.Id,
                message.AttemptCount,
                utcNow,
                cancellationToken);
            LogLeaseLossIfNeeded(updated, message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await HandleRetryableFailureAsync(message, exception, cancellationToken);
        }
    }

    private async Task<OutboxHandlerOutcome> HandleMessageAsync(
        OutboxProcessingItem message,
        CancellationToken cancellationToken)
    {
        if (message.AggregateType
            == RecoveryPlanLifecycleOutboxEventTypes.AggregateType)
        {
            return await HandleRecoveryPlanEventAsync(
                message,
                cancellationToken);
        }

        if (message.AggregateType == RecoveryPlanOutboxEventTypes.AggregateType)
        {
            return HandleRecoveryPlanRequestEvent(message);
        }

        return OutboxHandlerOutcome.TerminalFailure(InvalidAggregateError);
    }

    private async Task<OutboxHandlerOutcome> HandleRecoveryPlanEventAsync(
        OutboxProcessingItem message,
        CancellationToken cancellationToken)
    {
        if (message.AggregateType
            != RecoveryPlanLifecycleOutboxEventTypes.AggregateType)
        {
            return OutboxHandlerOutcome.TerminalFailure(InvalidAggregateError);
        }

        switch (message.EventType)
        {
            case RecoveryPlanLifecycleOutboxEventTypes.Ready:
                return await HandleRecoveryPlanNotificationAsync(
                    message,
                    RecoveryPlanStatus.ReadyToStart,
                    NotificationTypes.RecoveryPlanReady,
                    RecoveryPlanNotificationContent.ReadyTitle,
                    RecoveryPlanNotificationContent.ReadyMessage,
                    $"recovery-plan-ready:{message.AggregateId:N}",
                    cancellationToken);

            case RecoveryPlanLifecycleOutboxEventTypes.Completed:
                return await HandleRecoveryPlanNotificationAsync(
                    message,
                    RecoveryPlanStatus.Completed,
                    NotificationTypes.RecoveryPlanCompleted,
                    RecoveryPlanNotificationContent.CompletedTitle,
                    RecoveryPlanNotificationContent.CompletedMessage,
                    $"recovery-plan-completed:{message.AggregateId:N}",
                    cancellationToken);

            case RecoveryPlanLifecycleOutboxEventTypes.Activated:
                return HandleRecognizedNoOp(
                    message,
                    RecoveryPlanLifecycleOutboxEventTypes.AggregateType);

            default:
                return OutboxHandlerOutcome.TerminalFailure(UnsupportedEventError);
        }
    }

    private OutboxHandlerOutcome HandleRecoveryPlanRequestEvent(
        OutboxProcessingItem message)
    {
        if (message.AggregateType != RecoveryPlanOutboxEventTypes.AggregateType)
        {
            return OutboxHandlerOutcome.TerminalFailure(InvalidAggregateError);
        }

        switch (message.EventType)
        {
            case RecoveryPlanOutboxEventTypes.Created:
            case RecoveryPlanOutboxEventTypes.Claimed:
            case RecoveryPlanOutboxEventTypes.ReviewStarted:
            case RecoveryPlanOutboxEventTypes.Released:
            case RecoveryPlanOutboxEventTypes.Reopened:
            case RecoveryPlanOutboxEventTypes.MoreInformationRequested:
            case RecoveryPlanOutboxEventTypes.InformationProvided:
            case RecoveryPlanOutboxEventTypes.Rejected:
            case RecoveryPlanOutboxEventTypes.Cancelled:
                return HandleRecognizedNoOp(
                    message,
                    RecoveryPlanOutboxEventTypes.AggregateType);

            default:
                return OutboxHandlerOutcome.TerminalFailure(UnsupportedEventError);
        }
    }

    private async Task<OutboxHandlerOutcome> HandleRecoveryPlanNotificationAsync(
        OutboxProcessingItem message,
        RecoveryPlanStatus requiredStatus,
        string notificationType,
        string title,
        string body,
        string dedupeKey,
        CancellationToken cancellationToken)
    {
        if (message.AggregateType != RecoveryPlanLifecycleOutboxEventTypes.AggregateType)
        {
            return OutboxHandlerOutcome.TerminalFailure(InvalidAggregateError);
        }

        var plan = await _outboxRepository.GetRecoveryPlanNotificationDataAsync(
            message.AggregateId,
            cancellationToken);

        if (plan is null || !plan.IsUserEligible || plan.Status != requiredStatus)
        {
            _logger.LogDebug(
                "Outbox event {EventType} for aggregate {AggregateId} is no longer eligible.",
                message.EventType,
                message.AggregateId);
            return OutboxHandlerOutcome.Success;
        }

        var utcNow = DateTime.UtcNow;
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = plan.UserId,
            ReminderId = null,
            Title = title,
            Message = body,
            Channel = NotificationChannels.Email,
            Status = NotificationStatuses.Pending,
            SentAt = null,
            NotificationType = notificationType,
            ReferenceType = NotificationReferenceTypes.RecoveryPlan,
            ReferenceId = plan.PlanId,
            ScheduledAt = utcNow,
            AttemptCount = 0,
            LastError = null,
            DedupeKey = dedupeKey,
            CreatedAt = utcNow,
            UpdatedAt = null,
            IsDeleted = false,
            DeletedAt = null
        };

        var inserted = await _notificationRepository.TryInsertAsync(
            notification,
            cancellationToken);

        if (!inserted)
        {
            _logger.LogDebug(
                "Notification already exists for outbox event {EventType}, aggregate {AggregateId}.",
                message.EventType,
                message.AggregateId);
        }

        return OutboxHandlerOutcome.Success;
    }

    private OutboxHandlerOutcome HandleRecognizedNoOp(
        OutboxProcessingItem message,
        string expectedAggregateType)
    {
        if (message.AggregateType != expectedAggregateType)
        {
            return OutboxHandlerOutcome.TerminalFailure(InvalidAggregateError);
        }

        _logger.LogDebug(
            "Outbox event {EventType} for aggregate {AggregateId} is a recognized no-op.",
            message.EventType,
            message.AggregateId);
        return OutboxHandlerOutcome.Success;
    }

    private async Task HandleRetryableFailureAsync(
        OutboxProcessingItem message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Outbox event {EventType} for aggregate {AggregateId} failed with {FailureType}.",
            message.EventType,
            message.AggregateId,
            exception.GetType().Name);

        if (message.AttemptCount >= _options.MaxAttempts)
        {
            await MarkFailedAsync(message, ProcessingFailureError, cancellationToken);
            return;
        }

        var utcNow = DateTime.UtcNow;
        var retryAtUtc = RecoveryPlanJobRetrySchedule.GetRetryAtUtc(
            message.Id,
            message.AttemptCount,
            utcNow,
            _options);
        var updated = await _outboxRepository.ScheduleRetryAsync(
            message.Id,
            message.AttemptCount,
            retryAtUtc,
            ProcessingFailureError,
            cancellationToken);
        LogLeaseLossIfNeeded(updated, message);
    }

    private async Task MarkFailedAsync(
        OutboxProcessingItem message,
        string error,
        CancellationToken cancellationToken)
    {
        var updated = await _outboxRepository.MarkFailedAsync(
            message.Id,
            message.AttemptCount,
            error,
            cancellationToken);
        LogLeaseLossIfNeeded(updated, message);
    }

    private void LogLeaseLossIfNeeded(bool updated, OutboxProcessingItem message)
    {
        if (updated)
        {
            return;
        }

        _logger.LogWarning(
            "Outbox event {EventType} for aggregate {AggregateId} lost its processing lease.",
            message.EventType,
            message.AggregateId);
    }

    private sealed record OutboxHandlerOutcome(bool IsTerminalFailure, string? Error)
    {
        public static OutboxHandlerOutcome Success { get; } = new(false, null);

        public static OutboxHandlerOutcome TerminalFailure(string error)
        {
            return new OutboxHandlerOutcome(true, error);
        }
    }
}

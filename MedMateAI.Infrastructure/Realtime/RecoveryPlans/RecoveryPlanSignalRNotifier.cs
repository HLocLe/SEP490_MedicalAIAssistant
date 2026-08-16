using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.RecoveryPlans;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Infrastructure.Realtime.RecoveryPlans;

public sealed class RecoveryPlanSignalRNotifier : IRecoveryPlanRealtimeNotifier
{
    private const string QueueTargetCategory = "queue";
    private const string UserTargetCategory = "user";
    private const string DoctorTargetCategory = "doctor";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(
        RecoveryPlanRealtimeConstants.DefaultDeliveryTimeoutSeconds);

    private readonly IHubContext<RecoveryPlanHub, IRecoveryPlanHubClient> _hubContext;
    private readonly ILogger<RecoveryPlanSignalRNotifier> _logger;

    public RecoveryPlanSignalRNotifier(
        IHubContext<RecoveryPlanHub, IRecoveryPlanHubClient> hubContext,
        ILogger<RecoveryPlanSignalRNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task TryNotifyRequestChangedAsync(
        RecoveryPlanRequestRealtimeNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification.QueueChangeType != RecoveryPlanQueueChangeType.None)
        {
            var queueMessage = new RecoveryPlanQueueChangedMessage(
                notification.RequestId,
                notification.QueueChangeType,
                notification.DiseaseGroup,
                notification.Status,
                notification.RequestedAt,
                notification.OccurredAtUtc,
                notification.Version);

            await TrySendAsync(
                notification.EventType,
                notification.RequestId,
                QueueTargetCategory,
                () => _hubContext.Clients
                    .Group(RecoveryPlanRealtimeGroups.DoctorQueue)
                    .RecoveryPlanQueueChanged(queueMessage),
                cancellationToken);
        }

        var requestMessage = new RecoveryPlanRequestChangedMessage(
            notification.RequestId,
            notification.EventType,
            notification.DiseaseGroup,
            notification.Status,
            notification.RequestedAt,
            notification.OccurredAtUtc,
            notification.Version);

        await TrySendAsync(
            notification.EventType,
            notification.RequestId,
            UserTargetCategory,
            () => _hubContext.Clients
                .Group(RecoveryPlanRealtimeGroups.User(notification.UserId))
                .RecoveryPlanRequestChanged(requestMessage),
            cancellationToken);

        if (notification.TargetDoctorId.HasValue)
        {
            await TrySendAsync(
                notification.EventType,
                notification.RequestId,
                DoctorTargetCategory,
                () => _hubContext.Clients
                    .Group(RecoveryPlanRealtimeGroups.Doctor(
                        notification.TargetDoctorId.Value))
                    .RecoveryPlanRequestChanged(requestMessage),
                cancellationToken);
        }
    }

    public async Task TryNotifyPlanChangedAsync(
        RecoveryPlanLifecycleRealtimeNotification notification,
        CancellationToken cancellationToken)
    {
        var message = new RecoveryPlanChangedMessage(
            notification.PlanId,
            notification.RequestId,
            notification.EventType,
            notification.Status,
            notification.OccurredAtUtc,
            notification.PublishedAt,
            notification.ActivatedAt,
            notification.StartDate,
            notification.EndDate);

        await TrySendAsync(
            notification.EventType,
            notification.PlanId,
            UserTargetCategory,
            () => _hubContext.Clients
                .Group(RecoveryPlanRealtimeGroups.User(notification.UserId))
                .RecoveryPlanChanged(message),
            cancellationToken);

        if (notification.TargetDoctorId.HasValue)
        {
            await TrySendAsync(
                notification.EventType,
                notification.PlanId,
                DoctorTargetCategory,
                () => _hubContext.Clients
                    .Group(RecoveryPlanRealtimeGroups.Doctor(
                        notification.TargetDoctorId.Value))
                    .RecoveryPlanChanged(message),
                cancellationToken);
        }
    }

    public Task TryNotifyDoctorRealtimeAccessChangedAsync(
        Guid doctorUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var message = new RecoveryPlanRealtimeAccessChangedMessage(
            RecoveryPlanRealtimeConstants.DoctorAccessChangedEventType,
            occurredAtUtc,
            true);

        return TrySendAsync(
            RecoveryPlanRealtimeConstants.DoctorAccessChangedEventType,
            null,
            UserTargetCategory,
            () => _hubContext.Clients
                .Group(RecoveryPlanRealtimeGroups.User(doctorUserId))
                .RecoveryPlanRealtimeAccessChanged(message),
            cancellationToken);
    }

    private async Task TrySendAsync(
        string eventType,
        Guid? aggregateId,
        string targetCategory,
        Func<Task> send,
        CancellationToken cancellationToken)
    {
        try
        {
            await send().WaitAsync(DeliveryTimeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            LogDeliveryFailure(
                exception,
                eventType,
                aggregateId,
                targetCategory,
                "timeout");
        }
        catch (OperationCanceledException exception)
        {
            LogDeliveryFailure(
                exception,
                eventType,
                aggregateId,
                targetCategory,
                "cancelled");
        }
        catch (Exception exception)
        {
            LogDeliveryFailure(
                exception,
                eventType,
                aggregateId,
                targetCategory,
                "failure");
        }
    }

    private void LogDeliveryFailure(
        Exception exception,
        string eventType,
        Guid? aggregateId,
        string targetCategory,
        string failureType)
    {
        if (aggregateId.HasValue)
        {
            _logger.LogWarning(
                exception,
                "Recovery plan real-time delivery {FailureType} for {EventType}, aggregate {AggregateId}, target {TargetCategory}.",
                failureType,
                eventType,
                aggregateId.Value,
                targetCategory);
            return;
        }

        _logger.LogWarning(
            exception,
            "Recovery plan real-time delivery {FailureType} for {EventType}, target {TargetCategory}.",
            failureType,
            eventType,
            targetCategory);
    }
}

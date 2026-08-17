using MedMateAI.Application.Common;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Service;

internal static class RecoveryPlanRealtimeNotificationFactory
{
    public static RecoveryPlanRequestRealtimeNotification CreateRequestNotification(
        RecoveryPlanRequest request,
        string eventType,
        RecoveryPlanQueueChangeType queueChangeType,
        Guid? targetDoctorId,
        DateTime occurredAtUtc)
    {
        return new RecoveryPlanRequestRealtimeNotification(
            request.UserId,
            targetDoctorId,
            request.Id,
            eventType,
            queueChangeType,
            request.DiseaseGroup,
            request.Status,
            request.RequestedAt,
            occurredAtUtc,
            request.Version);
    }

    public static RecoveryPlanLifecycleRealtimeNotification CreatePlanNotification(
        RecoveryPlan plan,
        string eventType,
        DateTime occurredAtUtc)
    {
        if (!plan.RecoveryPlanRequestId.HasValue)
        {
            throw new InvalidOperationException(
                "A request-based recovery plan is required for real-time notification.");
        }

        return new RecoveryPlanLifecycleRealtimeNotification(
            plan.UserId,
            plan.DoctorId,
            plan.Id,
            plan.RecoveryPlanRequestId.Value,
            eventType,
            plan.Status,
            occurredAtUtc,
            plan.PublishedAt,
            plan.ActivatedAt,
            plan.StartDate,
            plan.EndDate);
    }
}

internal enum RecoveryPlanQueueChangeRule
{
    None,
    Added,
    RemovedWhenPreviouslyWaiting
}

internal sealed record RecoveryPlanRealtimeTransitionDescriptor(
    string EventType,
    RecoveryPlanQueueChangeRule QueueChangeRule)
{
    public RecoveryPlanQueueChangeType ResolveQueueChange(
        RecoveryPlanRequestStatus previousStatus)
    {
        return QueueChangeRule switch
        {
            RecoveryPlanQueueChangeRule.None => RecoveryPlanQueueChangeType.None,
            RecoveryPlanQueueChangeRule.Added => RecoveryPlanQueueChangeType.Added,
            RecoveryPlanQueueChangeRule.RemovedWhenPreviouslyWaiting
                when previousStatus == RecoveryPlanRequestStatus.WaitingForDoctor
                => RecoveryPlanQueueChangeType.Removed,
            RecoveryPlanQueueChangeRule.RemovedWhenPreviouslyWaiting
                => RecoveryPlanQueueChangeType.None,
            _ => throw new ArgumentOutOfRangeException(nameof(QueueChangeRule))
        };
    }
}

internal static class RecoveryPlanRealtimeTransitions
{
    public static readonly RecoveryPlanRealtimeTransitionDescriptor Cancelled = new(
        RecoveryPlanOutboxEventTypes.Cancelled,
        RecoveryPlanQueueChangeRule.RemovedWhenPreviouslyWaiting);

    public static readonly RecoveryPlanRealtimeTransitionDescriptor ReviewStarted = new(
        RecoveryPlanOutboxEventTypes.ReviewStarted,
        RecoveryPlanQueueChangeRule.None);

    public static readonly RecoveryPlanRealtimeTransitionDescriptor Released = new(
        RecoveryPlanOutboxEventTypes.Released,
        RecoveryPlanQueueChangeRule.Added);

    public static readonly RecoveryPlanRealtimeTransitionDescriptor Reopened = new(
        RecoveryPlanOutboxEventTypes.Reopened,
        RecoveryPlanQueueChangeRule.Added);

    public static readonly RecoveryPlanRealtimeTransitionDescriptor Rejected = new(
        RecoveryPlanOutboxEventTypes.Rejected,
        RecoveryPlanQueueChangeRule.None);
}

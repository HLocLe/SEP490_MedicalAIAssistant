using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Models.RecoveryPlans;

public enum RecoveryPlanQueueChangeType
{
    None,
    Added,
    Removed
}

public sealed record RecoveryPlanRequestRealtimeNotification(
    Guid UserId,
    Guid? TargetDoctorId,
    Guid RequestId,
    string EventType,
    RecoveryPlanQueueChangeType QueueChangeType,
    RecoveryPlanDiseaseGroup DiseaseGroup,
    RecoveryPlanRequestStatus Status,
    DateTime RequestedAt,
    DateTime OccurredAtUtc,
    int Version);

public sealed record RecoveryPlanLifecycleRealtimeNotification(
    Guid UserId,
    Guid? TargetDoctorId,
    Guid PlanId,
    Guid RequestId,
    string EventType,
    RecoveryPlanStatus Status,
    DateTime OccurredAtUtc,
    DateTime? PublishedAt,
    DateTime? ActivatedAt,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record RecoveryPlanQueueChangedMessage(
    Guid RequestId,
    RecoveryPlanQueueChangeType ChangeType,
    RecoveryPlanDiseaseGroup DiseaseGroup,
    RecoveryPlanRequestStatus Status,
    DateTime RequestedAt,
    DateTime OccurredAtUtc,
    int Version);

public sealed record RecoveryPlanRequestChangedMessage(
    Guid RequestId,
    string EventType,
    RecoveryPlanDiseaseGroup DiseaseGroup,
    RecoveryPlanRequestStatus Status,
    DateTime RequestedAt,
    DateTime OccurredAtUtc,
    int Version);

public sealed record RecoveryPlanChangedMessage(
    Guid PlanId,
    Guid RequestId,
    string EventType,
    RecoveryPlanStatus Status,
    DateTime OccurredAtUtc,
    DateTime? PublishedAt,
    DateTime? ActivatedAt,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record RecoveryPlanRealtimeAccessChangedMessage(
    string EventType,
    DateTime OccurredAtUtc,
    bool RefreshRequired);

public sealed record RecoveryPlanRealtimeDoctorAccess(
    Guid DoctorId,
    bool IsActive,
    bool IsAcceptingRecoveryPlanRequests,
    bool IsAccountValid);

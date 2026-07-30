using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Common;

public sealed record OutboxProcessingItem(
    Guid Id,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    int AttemptCount);

public sealed record RecoveryPlanNotificationData(
    Guid PlanId,
    Guid UserId,
    RecoveryPlanStatus Status,
    bool IsUserEligible);

public sealed record NotificationProcessingItem(
    Guid Id,
    Guid UserId,
    string NotificationType,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTime? ScheduledAt,
    int AttemptCount);

public sealed record NotificationRecipientData(
    Guid UserId,
    string? Email,
    string TimeZoneId,
    bool IsEligible);

public sealed record RecoveryPlanNotificationReferenceData(
    Guid PlanId,
    Guid UserId,
    RecoveryPlanStatus Status);

public sealed record MedicationReminderNotificationData(
    Guid ReminderTimeId,
    Guid UserId,
    string MedicineName,
    string? DosageInstruction,
    DateOnly? StartDate,
    DateOnly? EndDate,
    TimeOnly TimeOfDay,
    bool IsReminderActive,
    bool IsMedicationReminderEnabled);

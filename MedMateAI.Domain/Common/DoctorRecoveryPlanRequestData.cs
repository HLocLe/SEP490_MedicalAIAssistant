using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Common;

public sealed record DoctorRecoveryPlanRequestData(
    Guid Id,
    Guid UserId,
    Guid? AssignedDoctorId,
    RecoveryPlanDiseaseGroup DiseaseGroup,
    Guid? TreatmentJourneyId,
    Guid? PrimaryLabTestSessionId,
    RecoveryPlanRequestStatus Status,
    string? RequestNote,
    DateTime RequestedAt,
    DateTime? AcceptedAt,
    DateTime? ReviewStartedAt,
    DateTime? AssignmentExpiresAt,
    DateTime? RejectedAt,
    DateTime? CancelledAt,
    string? RejectionReasonCode,
    string? RejectionReason,
    int Version,
    Guid? RecoveryPlanId,
    RecoveryPlanStatus? RecoveryPlanStatus);

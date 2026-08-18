using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public abstract class RecoveryPlanRequestResponseBase
{
    public Guid Id { get; set; }

    public Guid? AssignedDoctorId { get; set; }

    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }

    public Guid? TreatmentJourneyId { get; set; }

    public Guid? PrimaryLabTestSessionId { get; set; }

    public RecoveryPlanRequestStatus Status { get; set; }

    public string? RequestNote { get; set; }

    public string? PrescriptionImageUrl { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? ReviewStartedAt { get; set; }

    public DateTime? AssignmentExpiresAt { get; set; }

    public DateTime? RejectedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? RejectionReasonCode { get; set; }

    public string? RejectionReason { get; set; }

    public int Version { get; set; }
}

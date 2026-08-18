using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlanRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? AssignedDoctorId { get; set; }
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public Guid? TreatmentJourneyId { get; set; }
    public Guid? PrimaryLabTestSessionId { get; set; }
    public Guid UserSubscriptionId { get; set; }
    public Guid UserSubscriptionUsageId { get; set; }
    public RecoveryPlanRequestStatus Status { get; set; }
    public string? RequestNote { get; set; }
    public string? PrescriptionImageUrl { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ReviewStartedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? AssignmentExpiresAt { get; set; }
    public string? RejectionReasonCode { get; set; }
    public string? RejectionReason { get; set; }
    public int Version { get; set; }
    public Doctor? AssignedDoctor { get; set; }
    public TreatmentJourney? TreatmentJourney { get; set; }
    public LabTestSession? PrimaryLabTestSession { get; set; }
    public UserSubscription UserSubscription { get; set; } = null!;
    public UserSubscriptionUsage UserSubscriptionUsage { get; set; } = null!;
    public RecoveryPlan? RecoveryPlan { get; set; }
    public ICollection<RecoveryPlanRequestEvent> Events { get; set; } = new List<RecoveryPlanRequestEvent>();
}

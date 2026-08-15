using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class RecoveryPlan : BaseEntity
{
    public Guid? TreatmentJourneyId { get; set; }

    public Guid? RecoveryPlanRequestId { get; set; }

    public Guid UserId { get; set; }

    public Guid? DoctorId { get; set; }

    public Guid? TestSessionId { get; set; }

    public Guid? SymptomAnalysisSessionId { get; set; }

    public string? PlanName { get; set; }

    public int DurationDays { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public string? Summary { get; set; }

    public RecoveryPlanStatus Status { get; set; } = RecoveryPlanStatus.Draft;

    public DateTime? PublishedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? FeedbackRating { get; set; }

    public string? FeedbackNote { get; set; }

    public DateTime? FeedbackSubmittedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public string? CancellationReasonCode { get; set; }

    public string? CancellationReason { get; set; }

    public string? RecheckInstruction { get; set; }

    public string? ClinicalSnapshotJson { get; set; }

    public TreatmentJourney? TreatmentJourney { get; set; }

    public RecoveryPlanRequest? RecoveryPlanRequest { get; set; }

    public Doctor? Doctor { get; set; }

    public LabTestSession? TestSession { get; set; }

    public SymptomAnalysisSession? SymptomAnalysisSession { get; set; }

    public ICollection<TreatmentLog> TreatmentLogs { get; set; } = new List<TreatmentLog>();

    public ICollection<AISystemConfig> AISystemConfigs { get; set; } = new List<AISystemConfig>();

    public ICollection<AIAnalysis> AIAnalyses { get; set; } = new List<AIAnalysis>();

    public ICollection<RecoveryPlanPhase> Phases { get; set; } = new List<RecoveryPlanPhase>();
}

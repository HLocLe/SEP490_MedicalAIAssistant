using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlans;

public sealed class CreateRecoveryPlanDraftRequest
{
    public string PlanName { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int DurationDays { get; set; }
    public string? RecheckInstruction { get; set; }
}

public sealed class UpdateRecoveryPlanDraftRequest
{
    public string PlanName { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int DurationDays { get; set; }
    public string? RecheckInstruction { get; set; }
}

public sealed class UpsertRecoveryPlanPhaseRequest
{
    public string PhaseName { get; set; } = string.Empty;
    public int StartDay { get; set; }
    public int EndDay { get; set; }
    public decimal? SleepAndRestHoursPerDay { get; set; }
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpsertRecoveryPlanNutrientTargetRequest
{
    public string NutrientName { get; set; } = string.Empty;
    public decimal AmountPerDay { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpsertRecoveryPlanFoodSourceRequest
{
    public string FoodName { get; set; } = string.Empty;
    public string? SuggestedServing { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

public sealed class RecoveryPlanSummaryResponse
{
    public Guid Id { get; set; }
    public Guid RecoveryPlanRequestId { get; set; }
    public string? PlanName { get; set; }
    public int DurationDays { get; set; }
    public RecoveryPlanStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? FeedbackRating { get; set; }
    public string? FeedbackNote { get; set; }
    public DateTime? FeedbackSubmittedAt { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class RecoveryPlanDetailResponse
{
    public Guid Id { get; set; }
    public Guid RecoveryPlanRequestId { get; set; }
    public string? PlanName { get; set; }
    public string? Summary { get; set; }
    public int DurationDays { get; set; }
    public RecoveryPlanStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? FeedbackRating { get; set; }
    public string? FeedbackNote { get; set; }
    public DateTime? FeedbackSubmittedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReasonCode { get; set; }
    public string? CancellationReason { get; set; }
    public string? RecheckInstruction { get; set; }
    public bool IsCurrent { get; set; }
    public IReadOnlyList<RecoveryPlanPhaseResponse> Phases { get; set; } =
        Array.Empty<RecoveryPlanPhaseResponse>();
}

public sealed class DoctorRecoveryPlanDetailResponse
{
    public RecoveryPlanDetailResponse Plan { get; set; } = new();
    public Guid RequestId { get; set; }
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public Guid? DoctorId { get; set; }
    public RecoveryPlanClinicalSnapshotResponse? ClinicalSnapshot { get; set; }
}

public sealed class RecoveryPlanClinicalSnapshotResponse
{
    public int SchemaVersion { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public Guid RequestId { get; set; }
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public RecoveryPlanSnapshotPatientProfileResponse? PatientProfile { get; set; }
    public IReadOnlyList<RecoveryPlanSnapshotChronicDiseaseResponse> ChronicDiseases { get; set; } =
        Array.Empty<RecoveryPlanSnapshotChronicDiseaseResponse>();
    public RecoveryPlanSnapshotPrimaryLabTestResponse? PrimaryLabTest { get; set; }
    public IReadOnlyList<RecoveryPlanSnapshotMedicationResponse> UserMedications { get; set; } =
        Array.Empty<RecoveryPlanSnapshotMedicationResponse>();
    public RecoveryPlanSnapshotTreatmentJourneyResponse? TreatmentJourney { get; set; }
}

public sealed class RecoveryPlanSnapshotPatientProfileResponse
{
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public double? Bmi { get; set; }
    public string? AllergyNote { get; set; }
    public DateTime ProfileCreatedAtUtc { get; set; }
    public DateTime? ProfileUpdatedAtUtc { get; set; }
}

public sealed class RecoveryPlanSnapshotChronicDiseaseResponse
{
    public string DiseaseName { get; set; } = string.Empty;
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Note { get; set; }
}

public sealed class RecoveryPlanSnapshotPrimaryLabTestResponse
{
    public Guid TestSessionId { get; set; }
    public string? FacilityName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public IReadOnlyList<RecoveryPlanSnapshotLabResultResponse> Results { get; set; } =
        Array.Empty<RecoveryPlanSnapshotLabResultResponse>();
}

public sealed class RecoveryPlanSnapshotLabResultResponse
{
    public Guid IndicatorId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Unit { get; set; }
    public double? UserValue { get; set; }
    public string? Status { get; set; }
    public double? MinReference { get; set; }
    public double? MaxReference { get; set; }
}

public sealed class RecoveryPlanSnapshotMedicationResponse
{
    public Guid UserMedicationId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? DosageInstruction { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Status { get; set; }
    public UserMedicationSourceType SourceType { get; set; }
}

public sealed class RecoveryPlanSnapshotTreatmentJourneyResponse
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? DiagnosisSummary { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Status { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? ApprovalNote { get; set; }
}

public sealed class RecoveryPlanPhaseResponse
{
    public Guid Id { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public int StartDay { get; set; }
    public int EndDay { get; set; }
    public decimal? SleepAndRestHoursPerDay { get; set; }
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
    public IReadOnlyList<RecoveryPlanNutrientTargetResponse> NutrientTargets { get; set; } =
        Array.Empty<RecoveryPlanNutrientTargetResponse>();
}

public sealed class RecoveryPlanNutrientTargetResponse
{
    public Guid Id { get; set; }
    public string NutrientName { get; set; } = string.Empty;
    public decimal AmountPerDay { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
    public IReadOnlyList<RecoveryPlanFoodSourceResponse> FoodSources { get; set; } =
        Array.Empty<RecoveryPlanFoodSourceResponse>();
}

public sealed class RecoveryPlanFoodSourceResponse
{
    public Guid Id { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? SuggestedServing { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

public sealed class RecoveryPlanClinicalContextResponse
{
    public Guid RequestId { get; set; }
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public Guid? TreatmentJourneyId { get; set; }
    public Guid? PrimaryLabTestSessionId { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? RequestNote { get; set; }
    public RecoveryPlanPatientProfileResponse? PatientProfile { get; set; }
    public IReadOnlyList<RecoveryPlanChronicDiseaseResponse> ChronicDiseases { get; set; } =
        Array.Empty<RecoveryPlanChronicDiseaseResponse>();
    public RecoveryPlanLabTestResponse? PrimaryLabTest { get; set; }
    public IReadOnlyList<RecoveryPlanUserMedicationResponse> UserMedications { get; set; } =
        Array.Empty<RecoveryPlanUserMedicationResponse>();
    public RecoveryPlanTreatmentJourneyResponse? TreatmentJourney { get; set; }
}

public sealed class RecoveryPlanPatientProfileResponse
{
    public double? Height { get; set; }
    public double? Weight { get; set; }
    public double? Bmi { get; set; }
    public string? AllergyNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class RecoveryPlanChronicDiseaseResponse
{
    public string DiseaseName { get; set; } = string.Empty;
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Note { get; set; }
}

public sealed class RecoveryPlanLabTestResponse
{
    public Guid TestSessionId { get; set; }
    public string? FacilityName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<RecoveryPlanLabResultResponse> Results { get; set; } =
        Array.Empty<RecoveryPlanLabResultResponse>();
}

public sealed class RecoveryPlanLabResultResponse
{
    public Guid IndicatorId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Unit { get; set; }
    public double? UserValue { get; set; }
    public string? Status { get; set; }
    public double? MinReference { get; set; }
    public double? MaxReference { get; set; }
}

public sealed class RecoveryPlanUserMedicationResponse
{
    public Guid UserMedicationId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? DosageInstruction { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Status { get; set; }
    public UserMedicationSourceType SourceType { get; set; }
}

public sealed class RecoveryPlanTreatmentJourneyResponse
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? DiagnosisSummary { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Status { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? ApprovalNote { get; set; }
}

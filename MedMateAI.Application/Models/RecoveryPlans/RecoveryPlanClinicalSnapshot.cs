using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Models.RecoveryPlans;

public sealed class RecoveryPlanClinicalSnapshot
{
    public int SchemaVersion { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public Guid RequestId { get; set; }
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public RecoveryPlanSnapshotPatientProfile? PatientProfile { get; set; }
    public IReadOnlyList<RecoveryPlanSnapshotChronicDisease> ChronicDiseases { get; set; } =
        Array.Empty<RecoveryPlanSnapshotChronicDisease>();
    public RecoveryPlanSnapshotPrimaryLabTest? PrimaryLabTest { get; set; }
    public IReadOnlyList<RecoveryPlanSnapshotMedication> UserMedications { get; set; } =
        Array.Empty<RecoveryPlanSnapshotMedication>();
    public RecoveryPlanSnapshotTreatmentJourney? TreatmentJourney { get; set; }
}

public sealed class RecoveryPlanSnapshotPatientProfile
{
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public double? Bmi { get; set; }
    public string? AllergyNote { get; set; }
    public DateTime ProfileCreatedAtUtc { get; set; }
    public DateTime? ProfileUpdatedAtUtc { get; set; }
}

public sealed class RecoveryPlanSnapshotChronicDisease
{
    public string DiseaseName { get; set; } = string.Empty;
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Note { get; set; }
}

public sealed class RecoveryPlanSnapshotPrimaryLabTest
{
    public Guid TestSessionId { get; set; }
    public string? FacilityName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public IReadOnlyList<RecoveryPlanSnapshotLabResult> Results { get; set; } =
        Array.Empty<RecoveryPlanSnapshotLabResult>();
}

public sealed class RecoveryPlanSnapshotLabResult
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

public sealed class RecoveryPlanSnapshotMedication
{
    public Guid UserMedicationId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? DosageInstruction { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Status { get; set; }
    public UserMedicationSourceType SourceType { get; set; }
}

public sealed class RecoveryPlanSnapshotTreatmentJourney
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

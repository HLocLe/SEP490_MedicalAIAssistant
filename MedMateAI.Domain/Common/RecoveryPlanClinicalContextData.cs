using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Common;

public sealed record RecoveryPlanClinicalContextData(
    Guid RequestId,
    Guid UserId,
    Guid? AssignedDoctorId,
    RecoveryPlanDiseaseGroup DiseaseGroup,
    RecoveryPlanRequestStatus RequestStatus,
    Guid? TreatmentJourneyId,
    Guid? PrimaryLabTestSessionId,
    DateTime RequestedAt,
    string? RequestNote,
    RecoveryPlanPatientProfileData? PatientProfile,
    IReadOnlyList<RecoveryPlanChronicDiseaseData> ChronicDiseases,
    RecoveryPlanLabTestData? PrimaryLabTest,
    IReadOnlyList<RecoveryPlanUserMedicationData> UserMedications,
    RecoveryPlanTreatmentJourneyData? TreatmentJourney);

public sealed record RecoveryPlanPatientProfileData(
    Guid PatientProfileId,
    double? HeightCm,
    double? WeightKg,
    string? AllergyNote,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record RecoveryPlanChronicDiseaseData(
    string DiseaseName,
    DateOnly? From,
    DateOnly? To,
    string? Note);

public sealed record RecoveryPlanLabTestData(
    Guid TestSessionId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<RecoveryPlanLabResultData> Results);

public sealed record RecoveryPlanLabResultData(
    Guid IndicatorId,
    string Symbol,
    string? FullName,
    string? Unit,
    double? UserValue,
    string? Status,
    double? MinReference,
    double? MaxReference);

public sealed record RecoveryPlanUserMedicationData(
    Guid UserMedicationId,
    string MedicineName,
    string? DosageInstruction,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Status,
    UserMedicationSourceType SourceType);

public sealed record RecoveryPlanTreatmentJourneyData(
    Guid Id,
    string? Title,
    string? DiagnosisSummary,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Status,
    DateTime? ApprovedAt,
    string? ApprovalStatus,
    string? ApprovalNote);

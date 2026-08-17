namespace MedMateAI.Domain.Common;

public sealed record RecoveryPlanRequestReadinessProfileData(
    Guid PatientProfileId,
    double? Height,
    double? Weight);

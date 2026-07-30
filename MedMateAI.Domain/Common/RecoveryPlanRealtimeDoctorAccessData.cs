namespace MedMateAI.Domain.Common;

public sealed record RecoveryPlanRealtimeDoctorAccessData(
    Guid DoctorId,
    bool IsActive,
    bool IsAcceptingRecoveryPlanRequests,
    bool IsAccountValid);

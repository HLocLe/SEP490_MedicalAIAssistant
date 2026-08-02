using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class DoctorRecoveryPlanRequestResponse : RecoveryPlanRequestResponseBase
{
    public Guid UserId { get; set; }

    public Guid? RecoveryPlanId { get; set; }

    public RecoveryPlanStatus? RecoveryPlanStatus { get; set; }
}

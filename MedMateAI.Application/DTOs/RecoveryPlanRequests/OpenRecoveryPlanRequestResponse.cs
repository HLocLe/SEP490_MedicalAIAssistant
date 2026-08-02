using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class OpenRecoveryPlanRequestResponse
{
    public Guid Id { get; set; }

    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }

    public RecoveryPlanRequestStatus Status { get; set; }

    public DateTime RequestedAt { get; set; }
}

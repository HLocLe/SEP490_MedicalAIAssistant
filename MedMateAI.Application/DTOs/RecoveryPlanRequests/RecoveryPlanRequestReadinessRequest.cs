using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class RecoveryPlanRequestReadinessRequest
{
    public RecoveryPlanDiseaseGroup? DiseaseGroup { get; set; }

    public string? RequestNote { get; set; }
}

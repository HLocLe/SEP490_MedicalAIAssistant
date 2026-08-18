using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class CreateRecoveryPlanRequest
{
    public RecoveryPlanDiseaseGroup? DiseaseGroup { get; set; }

    public Guid? TreatmentJourneyId { get; set; }

    public Guid? PrimaryLabTestSessionId { get; set; }

    public string? RequestNote { get; set; }

    public string? PrescriptionImageUrl { get; set; }
}

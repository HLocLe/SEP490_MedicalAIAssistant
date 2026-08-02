using System.ComponentModel.DataAnnotations;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class CreateRecoveryPlanRequest
{
    [EnumDataType(typeof(RecoveryPlanDiseaseGroup))]
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }

    public Guid? TreatmentJourneyId { get; set; }

    public Guid? PrimaryLabTestSessionId { get; set; }

    [MaxLength(2000)]
    public string? RequestNote { get; set; }
}

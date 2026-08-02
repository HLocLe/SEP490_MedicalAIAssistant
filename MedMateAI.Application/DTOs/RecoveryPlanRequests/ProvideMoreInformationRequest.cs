using System.ComponentModel.DataAnnotations;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class ProvideMoreInformationRequest
{
    [Required]
    [MaxLength(2000)]
    public string AdditionalInformation { get; set; } = string.Empty;
}

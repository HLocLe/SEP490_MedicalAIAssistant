using System.ComponentModel.DataAnnotations;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class RequestMoreInformationRequest
{
    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

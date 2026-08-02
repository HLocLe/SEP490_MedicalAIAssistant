using System.ComponentModel.DataAnnotations;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class RejectRecoveryPlanRequest
{
    [Required]
    [MaxLength(100)]
    public string RejectionReasonCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string RejectionReason { get; set; } = string.Empty;
}

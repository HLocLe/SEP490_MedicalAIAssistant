using System.ComponentModel.DataAnnotations;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class ReleaseRecoveryPlanRequest
{
    [MaxLength(2000)]
    public string? Reason { get; set; }
}

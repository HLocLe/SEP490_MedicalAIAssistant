namespace MedMateAI.Application.DTOs.RecoveryPlans;

public sealed class CancelRecoveryPlanRequest
{
    public string CancellationReasonCode { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
}

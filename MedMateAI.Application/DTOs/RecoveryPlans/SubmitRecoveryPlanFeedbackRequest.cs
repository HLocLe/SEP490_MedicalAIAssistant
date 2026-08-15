namespace MedMateAI.Application.DTOs.RecoveryPlans;

public sealed class SubmitRecoveryPlanFeedbackRequest
{
    public int Rating { get; set; }

    public string? Note { get; set; }
}

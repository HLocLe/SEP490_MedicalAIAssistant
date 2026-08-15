namespace MedMateAI.Application.DTOs.RecoveryPlans.Analytics;

public sealed class RecoveryPlanFeedbackTimelineResponse
{
    public string Period { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int FeedbackCount { get; set; }
}

namespace MedMateAI.Application.DTOs.RecoveryPlans.Analytics;

public sealed class RecoveryPlanFeedbackAnalyticsResponse
{
    public double AverageRating { get; set; }
    public int TotalFeedbacks { get; set; }
    public int CompletedPlans { get; set; }
    public double FeedbackRate { get; set; }
    public IReadOnlyList<RecoveryPlanRatingDistributionResponse> RatingDistribution { get; set; } =
        Array.Empty<RecoveryPlanRatingDistributionResponse>();
    public IReadOnlyList<RecoveryPlanFeedbackTimelineResponse> Timeline { get; set; } =
        Array.Empty<RecoveryPlanFeedbackTimelineResponse>();
}

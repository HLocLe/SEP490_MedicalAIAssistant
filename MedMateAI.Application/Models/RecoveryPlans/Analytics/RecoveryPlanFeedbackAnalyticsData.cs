namespace MedMateAI.Application.Models.RecoveryPlans.Analytics;

public sealed record RecoveryPlanFeedbackAnalyticsData(
    int CompletedPlans,
    IReadOnlyList<RecoveryPlanFeedbackData> Feedbacks);

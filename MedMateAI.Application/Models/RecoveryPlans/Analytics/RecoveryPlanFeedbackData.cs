namespace MedMateAI.Application.Models.RecoveryPlans.Analytics;

public sealed record RecoveryPlanFeedbackData(
    int Rating,
    DateTime SubmittedAt);

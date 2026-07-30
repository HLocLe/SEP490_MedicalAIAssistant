namespace MedMateAI.Domain.Common;

public sealed record RecoveryPlanCompletionCandidate(
    Guid PlanId,
    Guid UserId,
    DateOnly EndDate,
    string? TimeZoneId);

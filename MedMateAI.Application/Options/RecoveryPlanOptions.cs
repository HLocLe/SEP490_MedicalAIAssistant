namespace MedMateAI.Application.Options;

public sealed class RecoveryPlanOptions
{
    public const string SectionName = "RecoveryPlan";
    public int AssignmentTimeoutMinutes { get; set; } = 60;
}

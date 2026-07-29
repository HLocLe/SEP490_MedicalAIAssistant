namespace MedMateAI.Application.Common;

public static class RecoveryPlanLifecycleOutboxEventTypes
{
    public const string AggregateType = "RecoveryPlan";
    public const string Ready = "RecoveryPlanReady";
    public const string Activated = "RecoveryPlanActivated";
}

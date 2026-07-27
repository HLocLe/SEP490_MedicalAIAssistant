namespace MedMateAI.Application.Common;

public static class RecoveryPlanOutboxEventTypes
{
    public const string AggregateType = "RecoveryPlanRequest";
    public const string Created = "RecoveryPlanRequestCreated";
    public const string Claimed = "RecoveryPlanRequestClaimed";
    public const string ReviewStarted = "RecoveryPlanRequestReviewStarted";
    public const string Released = "RecoveryPlanRequestReleased";
    public const string MoreInformationRequested = "RecoveryPlanRequestMoreInformationRequested";
    public const string InformationProvided = "RecoveryPlanRequestInformationProvided";
    public const string Rejected = "RecoveryPlanRequestRejected";
    public const string Cancelled = "RecoveryPlanRequestCancelled";
}

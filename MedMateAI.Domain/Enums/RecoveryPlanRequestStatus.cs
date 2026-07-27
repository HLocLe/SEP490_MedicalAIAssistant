namespace MedMateAI.Domain.Enums;

public enum RecoveryPlanRequestStatus
{
    WaitingForDoctor,
    Assigned,
    InReview,
    NeedMoreInformation,
    Published,
    Rejected,
    Cancelled,
    Expired
}

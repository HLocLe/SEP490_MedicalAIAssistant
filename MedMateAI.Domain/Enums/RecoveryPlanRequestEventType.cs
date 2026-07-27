namespace MedMateAI.Domain.Enums;

public enum RecoveryPlanRequestEventType
{
    Created,
    QuotaReserved,
    Accepted,
    ReviewStarted,
    MoreInformationRequested,
    Released,
    Reopened,
    Published,
    Rejected,
    Cancelled,
    Expired
}

namespace MedMateAI.Domain.Enums;

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Processed,
    Failed
}

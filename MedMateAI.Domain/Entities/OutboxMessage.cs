using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}

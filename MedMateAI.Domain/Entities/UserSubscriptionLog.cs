using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class UserSubscriptionLog
{
    public Guid Id { get; set; }
    public Guid UserSubscriptionId { get; set; }
    public Guid UserSubscriptionUsageId { get; set; }
    public Guid QuotaId { get; set; }
    public SubscriptionQuotaActionType ActionType { get; set; }
    public int Quantity { get; set; }
    public int UsedCountBefore { get; set; }
    public int UsedCountAfter { get; set; }
    public int ReservedCountBefore { get; set; }
    public int ReservedCountAfter { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Reason { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public UserSubscription UserSubscription { get; set; } = null!;
    public UserSubscriptionUsage UserSubscriptionUsage { get; set; } = null!;
    public Quota Quota { get; set; } = null!;
}

namespace MedMateAI.Domain.Entities;

public sealed class UserSubscriptionUsage : BaseEntity
{
    public Guid UserSubscriptionId { get; set; }
    public Guid QuotaId { get; set; }
    public int LimitValue { get; set; }
    public int UsedCount { get; set; }
    public int ReservedCount { get; set; }
    public DateTime CycleStart { get; set; }
    public DateTime CycleEnd { get; set; }
    public int Version { get; set; }
    public UserSubscription UserSubscription { get; set; } = null!;
    public Quota Quota { get; set; } = null!;
    public ICollection<UserSubscriptionLog> Logs { get; set; } = new List<UserSubscriptionLog>();
    public ICollection<RecoveryPlanRequest> RecoveryPlanRequests { get; set; } = new List<RecoveryPlanRequest>();
}

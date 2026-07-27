namespace MedMateAI.Domain.Entities;

public sealed class Quota : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public ICollection<SubscriptionPlanQuota> SubscriptionPlanQuotas { get; set; } = new List<SubscriptionPlanQuota>();
    public ICollection<UserSubscriptionUsage> UserSubscriptionUsages { get; set; } = new List<UserSubscriptionUsage>();
    public ICollection<UserSubscriptionLog> UserSubscriptionLogs { get; set; } = new List<UserSubscriptionLog>();
}

namespace MedMateAI.Domain.Entities;

public sealed class SubscriptionPlan : BaseEntity
{
    public string? PlanName { get; set; }

    public decimal Price { get; set; }

    public int DurationInDays { get; set; }

    public string? FeatureLimitJson { get; set; }

    public bool IsActive { get; set; }

    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();

    public ICollection<SubscriptionPlanQuota> SubscriptionPlanQuotas { get; set; } = new List<SubscriptionPlanQuota>();

    public ICollection<SaleCampaignPlan> SaleCampaignPlans { get; set; } = new List<SaleCampaignPlan>();
}

using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class SaleRedemption : BaseEntity
{
    public Guid SaleCampaignId { get; set; }

    public Guid SaleCampaignPlanId { get; set; }

    public Guid UserId { get; set; }

    public Guid PlanId { get; set; }

    public Guid UserSubscriptionId { get; set; }

    public Guid PaymentId { get; set; }

    public string CampaignNameSnapshot { get; set; } = string.Empty;

    public string? BadgeTextSnapshot { get; set; }

    public SaleCampaignEligibilityType EligibilityTypeSnapshot { get; set; }

    public decimal OriginalPrice { get; set; }

    public decimal FinalPrice { get; set; }

    public int BaseCredit { get; set; }

    public int BonusCredit { get; set; }

    public int GrantedCredit { get; set; }

    public SaleRedemptionStatus Status { get; set; }

    public DateTime ReservedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public SaleCampaign SaleCampaign { get; set; } = null!;

    public SaleCampaignPlan SaleCampaignPlan { get; set; } = null!;

    public SubscriptionPlan Plan { get; set; } = null!;

    public UserSubscription UserSubscription { get; set; } = null!;

    public Payment Payment { get; set; } = null!;
}

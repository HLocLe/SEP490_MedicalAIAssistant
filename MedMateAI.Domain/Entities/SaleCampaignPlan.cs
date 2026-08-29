namespace MedMateAI.Domain.Entities;

public sealed class SaleCampaignPlan : BaseEntity
{
    public Guid SaleCampaignId { get; set; }

    public Guid PlanId { get; set; }

    public decimal? SalePrice { get; set; }

    public int BonusCredit { get; set; }

    public bool IsActive { get; set; }

    public SaleCampaign SaleCampaign { get; set; } = null!;

    public SubscriptionPlan Plan { get; set; } = null!;

    public ICollection<SaleRedemption> Redemptions { get; set; } =
        new List<SaleRedemption>();
}

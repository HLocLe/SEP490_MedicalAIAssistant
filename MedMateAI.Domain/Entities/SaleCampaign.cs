using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class SaleCampaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? BadgeText { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public SaleCampaignEligibilityType EligibilityType { get; set; }

    public int? MaxRedemptions { get; set; }

    public int? MaxRedemptionsPerUser { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public ICollection<SaleCampaignPlan> CampaignPlans { get; set; } =
        new List<SaleCampaignPlan>();

    public ICollection<SaleRedemption> Redemptions { get; set; } =
        new List<SaleRedemption>();
}

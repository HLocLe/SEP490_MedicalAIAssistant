using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Sales.Responses;

public sealed class SaleOfferResponse
{
    public Guid OfferId { get; set; }
    public Guid CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BadgeText { get; set; }
    public SaleCampaignEligibilityType EligibilityType { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public int BaseCredit { get; set; }
    public int BonusCredit { get; set; }
    public int GrantedCredit { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int? MaxRedemptions { get; set; }
    public int? RemainingRedemptions { get; set; }
    public int? MaxRedemptionsPerUser { get; set; }
}

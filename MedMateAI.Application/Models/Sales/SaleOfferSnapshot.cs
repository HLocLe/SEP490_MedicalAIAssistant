using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Models.Sales;

public sealed record SaleOfferSnapshot(
    Guid OfferId,
    Guid CampaignId,
    string CampaignName,
    string? Description,
    string? BadgeText,
    SaleCampaignEligibilityType EligibilityType,
    decimal OriginalPrice,
    decimal FinalPrice,
    int BaseCredit,
    int BonusCredit,
    int GrantedCredit,
    DateTime StartAt,
    DateTime EndAt,
    int? MaxRedemptions,
    int? RemainingRedemptions,
    int? MaxRedemptionsPerUser);

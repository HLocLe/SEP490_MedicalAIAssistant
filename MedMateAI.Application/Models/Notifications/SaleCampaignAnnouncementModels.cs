using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Models.Notifications;

public enum SaleCampaignBenefitType
{
    PriceOnly = 0,
    BonusOnly = 1,
    ComboOrMixed = 2
}

public sealed record SaleCampaignAnnouncementOffer(
    Guid PlanId,
    string? PlanName,
    decimal OriginalPrice,
    decimal EffectivePrice,
    int BaseCredit,
    int BonusCredit,
    int GrantedCredit);

public sealed record SaleCampaignAnnouncementContext(
    Guid UserId,
    string? Email,
    string? DisplayName,
    Guid CampaignId,
    string CampaignName,
    string? Description,
    string? BadgeText,
    SaleCampaignEligibilityType EligibilityType,
    DateTime EndAt,
    SaleCampaignBenefitType BenefitType,
    IReadOnlyList<SaleCampaignAnnouncementOffer> Offers);

public sealed record SaleCampaignNotificationContent(
    string Title,
    string Body);

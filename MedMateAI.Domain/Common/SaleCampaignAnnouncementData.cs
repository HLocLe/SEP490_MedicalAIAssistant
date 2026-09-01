using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Common;

public sealed record SaleCampaignAnnouncementRecipientData(
    Guid UserId,
    string? Email,
    string? DisplayName);

public sealed record SaleCampaignAnnouncementCampaignData(
    Guid CampaignId,
    string Name,
    string? Description,
    string? BadgeText,
    SaleCampaignEligibilityType EligibilityType,
    DateTime EndAt);

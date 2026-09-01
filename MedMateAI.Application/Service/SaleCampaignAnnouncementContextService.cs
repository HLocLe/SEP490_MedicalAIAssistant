using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Application.Service;

public sealed class SaleCampaignAnnouncementContextService :
    ISaleCampaignAnnouncementContextService
{
    private readonly ISaleCampaignAnnouncementRepository _repository;
    private readonly ISaleCampaignService _saleCampaignService;

    public SaleCampaignAnnouncementContextService(
        ISaleCampaignAnnouncementRepository repository,
        ISaleCampaignService saleCampaignService)
    {
        _repository = repository;
        _saleCampaignService = saleCampaignService;
    }

    public async Task<IReadOnlyList<SaleCampaignAnnouncementContext>>
        GetEligibleContextsAsync(
            SaleCampaignAnnouncementRecipientData recipient,
            IReadOnlyCollection<Guid> campaignIds,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
    {
        if (recipient.UserId == Guid.Empty || campaignIds.Count == 0)
        {
            return Array.Empty<SaleCampaignAnnouncementContext>();
        }

        utcNow = AsUtc(utcNow);
        var campaigns = await _repository.GetAnnounceableCampaignsAsync(
            campaignIds,
            utcNow,
            cancellationToken);
        if (campaigns.Count == 0)
        {
            return Array.Empty<SaleCampaignAnnouncementContext>();
        }

        var campaignById = campaigns.ToDictionary(campaign => campaign.CampaignId);
        var currentOffers = await _saleCampaignService.GetOffersAtAsync(
            recipient.UserId,
            utcNow,
            cancellationToken);
        var offersByCampaign = currentOffers
            .Where(item =>
                item.Offer is not null
                && campaignById.ContainsKey(item.Offer.CampaignId))
            .GroupBy(item => item.Offer!.CampaignId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var contexts = new List<SaleCampaignAnnouncementContext>();
        foreach (var campaign in campaigns)
        {
            if (!offersByCampaign.TryGetValue(
                    campaign.CampaignId,
                    out var resolvedOffers)
                || resolvedOffers.Count == 0)
            {
                continue;
            }

            var hasPriceDiscount = resolvedOffers.Any(offer =>
                offer.EffectivePrice < offer.OriginalPrice);
            var hasBonusCredit = resolvedOffers.Any(offer =>
                offer.GrantedCredit > offer.BaseCredit);
            if (!hasPriceDiscount && !hasBonusCredit)
            {
                continue;
            }

            var benefitType = hasPriceDiscount && hasBonusCredit
                ? SaleCampaignBenefitType.ComboOrMixed
                : hasPriceDiscount
                    ? SaleCampaignBenefitType.PriceOnly
                    : SaleCampaignBenefitType.BonusOnly;
            contexts.Add(new SaleCampaignAnnouncementContext(
                recipient.UserId,
                recipient.Email,
                recipient.DisplayName,
                campaign.CampaignId,
                campaign.Name,
                campaign.Description,
                campaign.BadgeText,
                campaign.EligibilityType,
                campaign.EndAt,
                benefitType,
                resolvedOffers.Select(offer =>
                    new SaleCampaignAnnouncementOffer(
                        offer.Plan.Id,
                        offer.Plan.PlanName,
                        offer.OriginalPrice,
                        offer.EffectivePrice,
                        offer.BaseCredit,
                        offer.BonusCredit,
                        offer.GrantedCredit)).ToList()));
        }

        return contexts;
    }

    public async Task<SaleCampaignAnnouncementContext?> GetEligibleContextAsync(
        Guid userId,
        Guid campaignId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || campaignId == Guid.Empty)
        {
            return null;
        }

        var recipient = await _repository.GetPatientRecipientAsync(
            userId,
            cancellationToken);
        if (recipient is null)
        {
            return null;
        }

        var contexts = await GetEligibleContextsAsync(
            recipient,
            new[] { campaignId },
            utcNow,
            cancellationToken);
        return contexts.SingleOrDefault();
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
    }
}

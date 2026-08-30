using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Sales;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class SaleRedemptionService : ISaleRedemptionService
{
    private readonly IUnitOfWork _unitOfWork;

    public SaleRedemptionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SaleReservationResult> ReserveBestOfferAsync(
        SubscriptionPlan lockedPlan,
        int baseCredit,
        Guid userId,
        Guid userSubscriptionId,
        Guid paymentId,
        Guid? expectedOfferId,
        decimal? expectedEffectivePrice,
        int? expectedGrantedCredit,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var hasExpectedPricingSnapshot =
            expectedOfferId.HasValue
            || expectedEffectivePrice.HasValue
            || expectedGrantedCredit.HasValue;

        if (!lockedPlan.IsActive
            || lockedPlan.IsDeleted
            || baseCredit <= 0
            || !TryConvertWholeVnd(lockedPlan.Price, out _))
        {
            return hasExpectedPricingSnapshot
                ? SaleReservationResult.Unavailable()
                : SaleReservationResult.NoOffer();
        }

        if (!await _unitOfWork.SaleRedemptions.LockUserAsync(userId, cancellationToken))
        {
            throw new InvalidOperationException("Authenticated user was not found.");
        }

        var candidates = await _unitOfWork.SaleCampaigns.GetOfferCandidatesAsync(
            new[] { lockedPlan.Id },
            utcNow,
            cancellationToken);
        var hasSuccessfulPurchase = await _unitOfWork.SaleRedemptions
            .HasSuccessfulPurchaseAsync(userId, cancellationToken);
        var hasFirstPurchaseReservation = await _unitOfWork.SaleRedemptions
            .HasFirstPurchaseReservationAsync(userId, cancellationToken);

        SaleOfferSnapshot? selected = null;
        foreach (var candidate in candidates)
        {
            var campaign = await _unitOfWork.SaleCampaigns.GetByIdForUpdateAsync(
                candidate.SaleCampaignId,
                cancellationToken);
            if (campaign is null
                || !campaign.IsActive
                || campaign.StartAt > utcNow
                || campaign.EndAt <= utcNow)
            {
                continue;
            }

            var campaignPlan = await _unitOfWork.SaleCampaigns.GetCampaignPlanAsync(
                campaign.Id,
                lockedPlan.Id,
                asNoTracking: false,
                cancellationToken);
            if (campaignPlan is null
                || !campaignPlan.IsActive
                || campaignPlan.IsDeleted
                || !HasValidBenefit(campaignPlan, lockedPlan.Price))
            {
                continue;
            }

            if (!IsEligible(
                    campaign.EligibilityType,
                    hasSuccessfulPurchase,
                    hasFirstPurchaseReservation))
            {
                continue;
            }

            var occupancyByCampaign = await _unitOfWork.SaleRedemptions.GetOccupancyAsync(
                new[] { campaign.Id },
                userId,
                cancellationToken);
            occupancyByCampaign.TryGetValue(campaign.Id, out var occupancy);
            var occupied = occupancy?.OccupiedCount ?? 0;
            var userOccupied = occupancy?.UserOccupiedCount ?? 0;
            if ((campaign.MaxRedemptions.HasValue
                    && occupied >= campaign.MaxRedemptions.Value)
                || (campaign.MaxRedemptionsPerUser.HasValue
                    && userOccupied >= campaign.MaxRedemptionsPerUser.Value))
            {
                continue;
            }

            var finalPrice = campaignPlan.SalePrice ?? lockedPlan.Price;
            int grantedCredit;
            try
            {
                grantedCredit = checked(baseCredit + campaignPlan.BonusCredit);
            }
            catch (OverflowException)
            {
                continue;
            }

            selected = new SaleOfferSnapshot(
                campaignPlan.Id,
                campaign.Id,
                campaign.Name,
                campaign.Description,
                campaign.BadgeText,
                campaign.EligibilityType,
                lockedPlan.Price,
                finalPrice,
                baseCredit,
                campaignPlan.BonusCredit,
                grantedCredit,
                campaign.StartAt,
                campaign.EndAt,
                campaign.MaxRedemptions,
                campaign.MaxRedemptions.HasValue
                    ? Math.Max(0, campaign.MaxRedemptions.Value - occupied - 1)
                    : null,
                campaign.MaxRedemptionsPerUser);
            break;
        }

        if (hasExpectedPricingSnapshot)
        {
            if (!expectedEffectivePrice.HasValue
                || !expectedGrantedCredit.HasValue)
            {
                return SaleReservationResult.Unavailable();
            }

            if (expectedOfferId.HasValue)
            {
                if (selected is null
                    || selected.OfferId != expectedOfferId.Value
                    || selected.FinalPrice != expectedEffectivePrice.Value
                    || selected.GrantedCredit != expectedGrantedCredit.Value)
                {
                    return SaleReservationResult.Unavailable();
                }
            }
            else if (selected is not null
                     || lockedPlan.Price != expectedEffectivePrice.Value
                     || baseCredit != expectedGrantedCredit.Value)
            {
                return SaleReservationResult.Unavailable();
            }
        }

        if (selected is null)
        {
            return SaleReservationResult.NoOffer();
        }

        var redemption = new SaleRedemption
        {
            Id = Guid.NewGuid(),
            SaleCampaignId = selected.CampaignId,
            SaleCampaignPlanId = selected.OfferId,
            UserId = userId,
            PlanId = lockedPlan.Id,
            UserSubscriptionId = userSubscriptionId,
            PaymentId = paymentId,
            CampaignNameSnapshot = selected.CampaignName,
            BadgeTextSnapshot = selected.BadgeText,
            EligibilityTypeSnapshot = selected.EligibilityType,
            OriginalPrice = selected.OriginalPrice,
            FinalPrice = selected.FinalPrice,
            BaseCredit = selected.BaseCredit,
            BonusCredit = selected.BonusCredit,
            GrantedCredit = selected.GrantedCredit,
            Status = SaleRedemptionStatus.Reserved,
            ReservedAt = utcNow,
            CreatedAt = utcNow
        };
        _unitOfWork.SaleRedemptions.Add(redemption);
        return SaleReservationResult.Reserved(selected, redemption);
    }

    public async Task<SaleRedemptionMutationStatus> CompleteAsync(
        Guid paymentId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var redemption = await _unitOfWork.SaleRedemptions.GetByPaymentIdForUpdateAsync(
            paymentId,
            cancellationToken);
        if (redemption is null)
        {
            return SaleRedemptionMutationStatus.NoRedemption;
        }

        if (redemption.Status == SaleRedemptionStatus.Completed)
        {
            return SaleRedemptionMutationStatus.Duplicate;
        }

        if (redemption.Status == SaleRedemptionStatus.Released)
        {
            return SaleRedemptionMutationStatus.Conflict;
        }

        redemption.Status = SaleRedemptionStatus.Completed;
        redemption.CompletedAt = utcNow;
        redemption.UpdatedAt = utcNow;
        return SaleRedemptionMutationStatus.Applied;
    }

    public async Task<SaleRedemptionMutationStatus> ReleaseAsync(
        Guid paymentId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var redemption = await _unitOfWork.SaleRedemptions.GetByPaymentIdForUpdateAsync(
            paymentId,
            cancellationToken);
        if (redemption is null)
        {
            return SaleRedemptionMutationStatus.NoRedemption;
        }

        if (redemption.Status == SaleRedemptionStatus.Released)
        {
            return SaleRedemptionMutationStatus.Duplicate;
        }

        if (redemption.Status == SaleRedemptionStatus.Completed)
        {
            return SaleRedemptionMutationStatus.Duplicate;
        }

        redemption.Status = SaleRedemptionStatus.Released;
        redemption.ReleasedAt = utcNow;
        redemption.UpdatedAt = utcNow;
        return SaleRedemptionMutationStatus.Applied;
    }

    private static bool IsEligible(
        SaleCampaignEligibilityType eligibilityType,
        bool hasSuccessfulPurchase,
        bool hasFirstPurchaseReservation)
    {
        return eligibilityType switch
        {
            SaleCampaignEligibilityType.All => true,
            SaleCampaignEligibilityType.FirstPurchase =>
                !hasSuccessfulPurchase && !hasFirstPurchaseReservation,
            SaleCampaignEligibilityType.ReturningCustomer => hasSuccessfulPurchase,
            _ => false
        };
    }

    private static bool HasValidBenefit(
        SaleCampaignPlan campaignPlan,
        decimal basePrice)
    {
        if (campaignPlan.BonusCredit < 0
            || (!campaignPlan.SalePrice.HasValue && campaignPlan.BonusCredit <= 0))
        {
            return false;
        }

        return !campaignPlan.SalePrice.HasValue
            || (campaignPlan.SalePrice.Value < basePrice
                && TryConvertWholeVnd(campaignPlan.SalePrice.Value, out _));
    }

    private static bool TryConvertWholeVnd(decimal amount, out int value)
    {
        value = 0;
        if (amount <= 0
            || amount != decimal.Truncate(amount)
            || amount > int.MaxValue)
        {
            return false;
        }

        value = decimal.ToInt32(amount);
        return true;
    }
}

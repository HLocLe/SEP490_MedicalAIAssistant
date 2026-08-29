using MedMateAI.Application.Models.Sales;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.IService;

public interface ISaleRedemptionService
{
    Task<SaleReservationResult> ReserveBestOfferAsync(
        SubscriptionPlan lockedPlan,
        int baseCredit,
        Guid userId,
        Guid userSubscriptionId,
        Guid paymentId,
        Guid? expectedOfferId,
        decimal? expectedEffectivePrice,
        int? expectedGrantedCredit,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<SaleRedemptionMutationStatus> CompleteAsync(
        Guid paymentId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<SaleRedemptionMutationStatus> ReleaseAsync(
        Guid paymentId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

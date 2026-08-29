using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface ISaleRedemptionRepository
{
    Task<bool> LockUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasSuccessfulPurchaseAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasFirstPurchaseReservationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, SaleRedemptionOccupancy>> GetOccupancyAsync(
        IReadOnlyCollection<Guid> campaignIds,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<int> GetHighestUserOccupiedCountAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<bool> HasHistoryAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<SaleRedemption?> GetByPaymentIdForUpdateAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<SaleRedemption>> GetPagedByCampaignAsync(
        Guid campaignId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    void Add(SaleRedemption redemption);
}

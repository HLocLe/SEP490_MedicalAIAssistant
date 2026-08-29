using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface ISaleCampaignRepository
{
    Task<PagedResult<SaleCampaign>> GetAdminPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SaleCampaign?> GetByIdWithDetailsAsync(
        Guid campaignId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<SaleCampaign?> GetByIdForUpdateAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleCampaignPlan>> GetOfferCandidatesAsync(
        IReadOnlyCollection<Guid> planIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<SaleCampaignPlan?> GetCampaignPlanAsync(
        Guid campaignId,
        Guid planId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    void Add(SaleCampaign campaign);
}

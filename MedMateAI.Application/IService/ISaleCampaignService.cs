using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Sales.Requests;
using MedMateAI.Application.DTOs.Sales.Responses;

namespace MedMateAI.Application.IService;

public interface ISaleCampaignService
{
    Task<PagedResponse<SaleCampaignResponse>> GetAdminCampaignsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SaleCampaignResponse?> GetByIdAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<SaleCampaignResponse> CreateAsync(
        UpsertSaleCampaignRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleCampaignResponse?> UpdateAsync(
        Guid campaignId,
        UpsertSaleCampaignRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleCampaignResponse?> UpdateStatusAsync(
        Guid campaignId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<SaleRedemptionResponse>?> GetRedemptionsAsync(
        Guid campaignId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlanOfferResponse>> GetOffersAsync(
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlanOfferResponse>> GetOffersAtAsync(
        Guid? userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

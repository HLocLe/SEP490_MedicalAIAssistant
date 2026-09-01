using MedMateAI.Domain.Common;

namespace MedMateAI.Domain.Repository;

public interface ISaleCampaignAnnouncementRepository
{
    Task<IReadOnlyList<SaleCampaignAnnouncementCampaignData>>
        GetAnnounceableCampaignPageAsync(
            DateTime utcNow,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleCampaignAnnouncementCampaignData>>
        GetAnnounceableCampaignsAsync(
            IReadOnlyCollection<Guid> campaignIds,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleCampaignAnnouncementRecipientData>>
        GetPatientRecipientPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

    Task<SaleCampaignAnnouncementRecipientData?> GetPatientRecipientAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

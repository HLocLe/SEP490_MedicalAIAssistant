using MedMateAI.Application.Models.Notifications;
using MedMateAI.Domain.Common;

namespace MedMateAI.Application.IService;

public interface ISaleCampaignAnnouncementContextService
{
    Task<IReadOnlyList<SaleCampaignAnnouncementContext>> GetEligibleContextsAsync(
        SaleCampaignAnnouncementRecipientData recipient,
        IReadOnlyCollection<Guid> campaignIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<SaleCampaignAnnouncementContext?> GetEligibleContextAsync(
        Guid userId,
        Guid campaignId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

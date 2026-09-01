using MedMateAI.Application.Models.Notifications;

namespace MedMateAI.Application.IService;

public interface ISaleCampaignNotificationContentBuilder
{
    SaleCampaignNotificationContent Build(
        SaleCampaignAnnouncementContext context,
        string channel);
}

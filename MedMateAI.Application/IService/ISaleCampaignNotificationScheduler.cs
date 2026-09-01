namespace MedMateAI.Application.IService;

public interface ISaleCampaignNotificationScheduler
{
    Task ScheduleAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

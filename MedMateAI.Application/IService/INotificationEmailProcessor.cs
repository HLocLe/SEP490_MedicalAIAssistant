namespace MedMateAI.Application.IService;

public interface INotificationEmailProcessor
{
    Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

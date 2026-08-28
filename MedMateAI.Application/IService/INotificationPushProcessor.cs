namespace MedMateAI.Application.IService;

public interface INotificationPushProcessor
{
    Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

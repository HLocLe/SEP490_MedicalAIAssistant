namespace MedMateAI.Application.IService;

public interface INotificationPushReceiptProcessor
{
    Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

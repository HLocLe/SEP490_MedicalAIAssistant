namespace MedMateAI.Application.IService;

public interface IOutboxMessageProcessor
{
    Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

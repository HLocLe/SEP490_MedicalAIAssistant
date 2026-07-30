namespace MedMateAI.Application.IService;

public interface IRecoveryPlanAssignmentTimeoutProcessor
{
    Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

public interface IRecoveryPlanCompletionProcessor
{
    Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

namespace MedMateAI.Application.IService;

public interface ISubscriptionPlanCacheInvalidator
{
    Task InvalidateAsync(
        Guid planId,
        CancellationToken cancellationToken = default);
}

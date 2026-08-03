using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class SubscriptionPlanCacheInvalidator
    : ISubscriptionPlanCacheInvalidator
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<SubscriptionPlanCacheInvalidator> _logger;

    public SubscriptionPlanCacheInvalidator(
        IDistributedCache cache,
        ILogger<SubscriptionPlanCacheInvalidator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task InvalidateAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var cacheKeys = new[]
        {
            SubscriptionPlanCacheKeys.All,
            SubscriptionPlanCacheKeys.Active,
            SubscriptionPlanCacheKeys.ForPlan(planId),
        };

        foreach (var cacheKey in cacheKeys)
        {
            try
            {
                await _cache.RemoveAsync(cacheKey, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Subscription plan cache invalidation failed for category {ErrorCategory}.",
                    ex.GetType().Name);
            }
        }
    }
}

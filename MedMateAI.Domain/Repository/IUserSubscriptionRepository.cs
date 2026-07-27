using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IUserSubscriptionRepository : IGenericRepository<UserSubscription>
{
    Task<UserSubscription?> GetByIdWithPlanAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<UserSubscription?> GetCurrentActiveByUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSubscription>> GetByUserWithPlanAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserSubscription?> GetCurrentActiveWithPlanQuotasAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

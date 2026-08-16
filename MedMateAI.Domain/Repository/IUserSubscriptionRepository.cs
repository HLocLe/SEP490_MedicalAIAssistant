using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

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

    Task<PagedResult<UserSubscription>> GetAdminPagedAsync(
        int pageNumber,
        int pageSize,
        SubscriptionStatus? status,
        bool currentOnly,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

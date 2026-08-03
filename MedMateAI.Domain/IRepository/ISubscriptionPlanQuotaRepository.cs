using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface ISubscriptionPlanQuotaRepository
{
    Task<IReadOnlyList<Quota>> ListQuotaDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<Quota?> GetQuotaDefinitionAsync(
        Guid quotaId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlanQuota>> ListPlanQuotasAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlanQuota>> ListActivePlanQuotasAsync(
        IReadOnlyCollection<Guid> planIds,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlan?> GetPlanForUpdateAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlanQuota?> GetNonDeletedMappingAsync(
        Guid planId,
        Guid quotaId,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlanQuota?> GetLatestDeletedMappingAsync(
        Guid planId,
        Guid quotaId,
        CancellationToken cancellationToken = default);

    void Add(SubscriptionPlanQuota mapping);
}

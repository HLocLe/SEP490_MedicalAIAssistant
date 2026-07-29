using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Repository;

public interface IRecoveryPlanRepository
{
    Task<RecoveryPlan?> GetByIdAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlan?> GetDetailByIdAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlan?> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetRequestIdByPlanIdAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlan?> GetByIdForUpdateAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlan?> GetTrackedDetailAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<RecoveryPlan>> GetUserPlansPagedAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        RecoveryPlanStatus? status,
        CancellationToken cancellationToken = default);

    Task<string?> GetUserTimeZoneIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlanClinicalContextData?> GetClinicalContextAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    void Add(RecoveryPlan plan);

    void AddOutbox(OutboxMessage message);
}

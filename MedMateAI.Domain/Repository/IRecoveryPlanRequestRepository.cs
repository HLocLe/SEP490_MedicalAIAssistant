using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Repository;

public interface IRecoveryPlanRequestRepository
{
    Task<RecoveryPlanRequest?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken = default);
    Task<RecoveryPlanRequest?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RecoveryPlanRequest>> GetOpenPagedAsync(int page, int size, RecoveryPlanDiseaseGroup? group, CancellationToken cancellationToken = default);
    Task<PagedResult<RecoveryPlanRequest>> GetByUserPagedAsync(Guid userId, int page, int size, RecoveryPlanRequestStatus? status, CancellationToken cancellationToken = default);
    Task<PagedResult<RecoveryPlanRequest>> GetAssignedPagedAsync(Guid doctorId, int page, int size, RecoveryPlanRequestStatus? status, CancellationToken cancellationToken = default);
    Task<RecoveryPlanRequest?> TryAcceptAsync(Guid requestId, Guid doctorId, DateTime now, DateTime expiresAt, CancellationToken cancellationToken = default);
    Task<Doctor?> GetDoctorForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountActiveAssignmentsAsync(Guid doctorId, CancellationToken cancellationToken = default);
    Task<bool> IsOwnedTreatmentJourneyAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsOwnedLabSessionAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    void Add(RecoveryPlanRequest request);
    void AddEvent(RecoveryPlanRequestEvent requestEvent);
    void AddOutbox(OutboxMessage message);
}

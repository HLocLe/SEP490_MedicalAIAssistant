using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IUserMedicationRepository
{
    Task<IReadOnlyList<UserMedication>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<UserMedication>> GetByUserIdPagedAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<UserMedication?> GetByIdAsync(
        Guid userId,
        Guid medicationId,
        CancellationToken cancellationToken = default);

    Task<UserMedication?> GetByIdForUpdateAsync(
        Guid userId,
        Guid medicationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicationReminderScheduleData>> GetActiveSchedulesAsync(
        DateOnly earliestLocalDate,
        DateOnly latestLocalDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    void Add(UserMedication medication);
}

using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.UserMedications;
using MedMateAI.Application.Models.UserMedications;

namespace MedMateAI.Application.IService;

public interface IUserMedicationService
{
    Task<UserMedicationOperationResult<IReadOnlyList<UserMedicationResponse>>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserMedicationOperationResult<PagedResponse<UserMedicationResponse>>> GetMinePagedAsync(
        Guid userId,
        PaginationQuery query,
        CancellationToken cancellationToken = default);

    Task<UserMedicationOperationResult<UserMedicationResponse>> GetByIdAsync(
        Guid userId,
        Guid medicationId,
        CancellationToken cancellationToken = default);

    Task<UserMedicationOperationResult<UserMedicationResponse>> CreateAsync(
        Guid userId,
        CreateUserMedicationRequest request,
        CancellationToken cancellationToken = default);

    Task<UserMedicationOperationResult<UserMedicationResponse>> UpdateAsync(
        Guid userId,
        Guid medicationId,
        UpdateUserMedicationRequest request,
        CancellationToken cancellationToken = default);

    Task<UserMedicationOperationResult<bool>> DeleteAsync(
        Guid userId,
        Guid medicationId,
        CancellationToken cancellationToken = default);

    Task<UserMedicationOperationResult<UserMedicationResponse>> ReplaceReminderTimesAsync(
        Guid userId,
        Guid medicationId,
        ReplaceMedicationReminderTimesRequest request,
        CancellationToken cancellationToken = default);
}

public interface IMedicationReminderScheduler
{
    Task ScheduleAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

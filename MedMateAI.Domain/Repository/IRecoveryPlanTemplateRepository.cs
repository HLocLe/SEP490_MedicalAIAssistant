using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Repository;

public interface IRecoveryPlanTemplateRepository
{
    Task<PagedResult<RecoveryPlanTemplate>> GetPagedAsync(
        Guid doctorId,
        int pageNumber,
        int pageSize,
        RecoveryPlanDiseaseGroup? diseaseGroup,
        string? search,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlanTemplate?> GetDetailAsync(
        Guid doctorId,
        Guid templateId,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlanTemplate?> GetByIdForUpdateAsync(
        Guid doctorId,
        Guid templateId,
        CancellationToken cancellationToken = default);

    Task<RecoveryPlanTemplate?> GetTrackedDetailAsync(
        Guid doctorId,
        Guid templateId,
        CancellationToken cancellationToken = default);

    void Add(RecoveryPlanTemplate template);
}

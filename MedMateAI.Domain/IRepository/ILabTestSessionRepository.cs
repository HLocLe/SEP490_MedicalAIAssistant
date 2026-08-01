using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Domain.Repository;

public interface ILabTestSessionRepository : IGenericRepository<LabTestSession>
{
    Task<LabTestSession?> GetByIdWithResultsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<LabTestSession>> GetPagedByUserIdAsync(
        Guid userId,
        LabTestSessionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}

using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Domain.Repository;

public interface ILabTestSessionRepository : IGenericRepository<LabTestSession>
{
    Task<LabTestSession?> GetByIdWithResultsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IDiseasePriorProbabilityRepository : IGenericRepository<DiseasePriorProbability>
{
    Task<IReadOnlyList<DiseasePriorProbability>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);
}

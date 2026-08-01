using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface ILabIndicatorRepository : IGenericRepository<LabIndicatorMaster>
{
    Task<LabIndicatorMaster?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> SymbolExistsAsync(
        string symbol,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LabIndicatorMaster>> GetAllActiveWithDetailsAsync(
        CancellationToken cancellationToken = default);
}

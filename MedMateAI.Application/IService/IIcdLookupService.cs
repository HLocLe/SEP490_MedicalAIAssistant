using MedMateAI.Application.DTOs.IcdLookup;

namespace MedMateAI.Application.IService;

public interface IIcdLookupService
{
    Task<IcdLookupResult?> SearchFirstAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);
}

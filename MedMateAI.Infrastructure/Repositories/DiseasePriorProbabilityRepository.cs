using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class DiseasePriorProbabilityRepository
    : GenericRepository<DiseasePriorProbability>, IDiseasePriorProbabilityRepository
{
    private readonly ApplicationDbContext _context;

    public DiseasePriorProbabilityRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DiseasePriorProbability>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.DiseasePriorProbabilities
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .ToListAsync(cancellationToken);
    }
}

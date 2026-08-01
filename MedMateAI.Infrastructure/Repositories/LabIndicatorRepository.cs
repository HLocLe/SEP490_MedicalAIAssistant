using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class LabIndicatorRepository
    : GenericRepository<LabIndicatorMaster>, ILabIndicatorRepository
{
    private readonly ApplicationDbContext _context;

    public LabIndicatorRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<LabIndicatorMaster?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.LabIndicatorMasters
            .AsNoTracking()
            .Include(x => x.LabIndicatorAliases.Where(a => !a.IsDeleted))
            .Include(x => x.LabIndicatorReferenceRanges.Where(r => !r.IsDeleted))
            .Include(x => x.LabIndicatorAdviceCaches.Where(c => !c.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public async Task<bool> SymbolExistsAsync(
        string symbol,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = symbol.Trim().ToUpperInvariant();

        return await _context.LabIndicatorMasters.AnyAsync(
            x => !x.IsDeleted
                 && x.Symbol.ToUpper() == normalized
                 && (!excludedId.HasValue || x.Id != excludedId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<LabIndicatorMaster>> GetAllActiveWithDetailsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.LabIndicatorMasters
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .Include(x => x.LabIndicatorAliases.Where(a => !a.IsDeleted))
            .Include(x => x.LabIndicatorReferenceRanges.Where(r => !r.IsDeleted))
            .Include(x => x.LabIndicatorAdviceCaches.Where(c => !c.IsDeleted))
            .ToListAsync(cancellationToken);
    }
}

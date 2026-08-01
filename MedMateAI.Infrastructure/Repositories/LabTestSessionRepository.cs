using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class LabTestSessionRepository
    : GenericRepository<LabTestSession>, ILabTestSessionRepository
{
    private readonly ApplicationDbContext _context;

    public LabTestSessionRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<LabTestSession?> GetByIdWithResultsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.LabTestSessions
            .AsNoTracking()
            .Include(x => x.LabTestResultDetails.Where(d => !d.IsDeleted))
                .ThenInclude(d => d.Indicator)
            .Include(x => x.LabTestResultDetails.Where(d => !d.IsDeleted))
                .ThenInclude(d => d.AdviceCache)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }
}

using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
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

    public async Task<PagedResult<LabTestSession>> GetPagedByUserIdAsync(
        Guid userId,
        LabTestSessionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : pageSize;
        normalizedPageSize = normalizedPageSize > 100 ? 100 : normalizedPageSize;

        var query = _context.LabTestSessions
            .AsNoTracking()
            .Where(session => !session.IsDeleted && session.UserId == userId);

        if (status.HasValue)
        {
            query = query.Where(session => session.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(session => session.CreatedAt)
            .ThenByDescending(session => session.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new PagedResult<LabTestSession>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items,
        };
    }
}

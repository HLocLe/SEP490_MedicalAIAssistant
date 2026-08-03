using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class SymptomAnalysisSessionRepository
    : GenericRepository<SymptomAnalysisSession>, ISymptomAnalysisSessionRepository
{
    private readonly ApplicationDbContext _context;

    public SymptomAnalysisSessionRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<PagedResult<SymptomAnalysisSession>> GetPagedByUserIdAsync(
        Guid userId,
        SymptomAnalysisSessionType? sessionType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : pageSize;
        normalizedPageSize = normalizedPageSize > 100 ? 100 : normalizedPageSize;

        var query = _context.SymptomAnalysisSessions
            .AsNoTracking()
            .Where(session => !session.IsDeleted && session.UserId == userId);

        if (sessionType.HasValue)
        {
            query = query.Where(session => session.SessionType == sessionType.Value);
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

        return new PagedResult<SymptomAnalysisSession>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items,
        };
    }
}

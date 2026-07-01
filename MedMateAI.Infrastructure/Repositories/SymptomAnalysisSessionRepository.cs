using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
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

    public async Task<IReadOnlyList<Guid>> DetachChapterCodeAsync(
        string chapterCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chapterCode))
        {
            return Array.Empty<Guid>();
        }

        var ids = await _context.SymptomAnalysisSessions
            .Where(session => session.ChapterCode == chapterCode)
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return ids;
        }

        var updatedAt = DateTime.UtcNow;
        await _context.SymptomAnalysisSessions
            .Where(session => ids.Contains(session.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.ChapterCode, (string?)null)
                    .SetProperty(session => session.UpdatedAt, updatedAt),
                cancellationToken);

        return ids;
    }

    public async Task AttachChapterCodeAsync(
        IReadOnlyList<Guid> ids,
        string chapterCode,
        CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return;
        }

        var updatedAt = DateTime.UtcNow;
        await _context.SymptomAnalysisSessions
            .Where(session => ids.Contains(session.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.ChapterCode, chapterCode)
                    .SetProperty(session => session.UpdatedAt, updatedAt),
                cancellationToken);
    }
}

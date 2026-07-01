using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class ClinicalQuestionRepository
    : GenericRepository<ClinicalQuestion>, IClinicalQuestionRepository
{
    private readonly ApplicationDbContext _context;

    public ClinicalQuestionRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ClinicalQuestion>> GetActiveQuestionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.ClinicalQuestions
            .AsNoTracking()
            .Where(question => !question.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClinicalQuestion>> GetQuestionsByChapterIdsAsync(
        IReadOnlyList<Guid> chapterIds,
        CancellationToken cancellationToken = default)
    {
        if (chapterIds is null || chapterIds.Count == 0)
        {
            return Array.Empty<ClinicalQuestion>();
        }

        return await _context.ClinicalQuestions
            .AsNoTracking()
            .Where(question => !question.IsDeleted
                && question.ChapterId.HasValue
                && chapterIds.Contains(question.ChapterId.Value))
            .OrderBy(question => question.ChapterId)
            .ThenBy(question => question.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClinicalQuestion>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return Array.Empty<ClinicalQuestion>();
        }

        return await _context.ClinicalQuestions
            .AsNoTracking()
            .Where(question => !question.IsDeleted && ids.Contains(question.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IcdChapterExistsAsync(Guid icdChapterId, CancellationToken cancellationToken = default)
    {
        return _context.IcdChapters
            .AsNoTracking()
            .AnyAsync(chapter => chapter.Id == icdChapterId && !chapter.IsDeleted, cancellationToken);
    }

    public async Task UpdateChapterCodeByChapterIdAsync(
        Guid chapterId,
        string chapterCode,
        CancellationToken cancellationToken = default)
    {
        var updatedAt = DateTime.UtcNow;

        await _context.ClinicalQuestions
            .Where(question => question.ChapterId == chapterId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(question => question.ChapterCode, chapterCode)
                    .SetProperty(question => question.UpdatedAt, updatedAt),
                cancellationToken);
    }
}

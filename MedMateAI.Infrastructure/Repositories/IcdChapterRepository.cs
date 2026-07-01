using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class IcdChapterRepository
    : GenericRepository<IcdChapter>, IIcdChapterRepository
{
    private readonly ApplicationDbContext _context;

    public IcdChapterRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<IcdChapter>> GetActiveChaptersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.IcdChapters
            .AsNoTracking()
            .Where(chapter => !chapter.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateChapterCodeByIdAsync(
        Guid id,
        string chapterCode,
        CancellationToken cancellationToken = default)
    {
        await _context.IcdChapters
            .Where(chapter => chapter.Id == id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(chapter => chapter.ChapterCode, chapterCode),
                cancellationToken);
    }
}

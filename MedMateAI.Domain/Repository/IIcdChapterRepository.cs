using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IIcdChapterRepository : IGenericRepository<IcdChapter>
{
    Task<IReadOnlyList<IcdChapter>> GetActiveChaptersAsync(CancellationToken cancellationToken = default);

    Task UpdateChapterCodeByIdAsync(
        Guid id,
        string chapterCode,
        CancellationToken cancellationToken = default);
}

using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IClinicalQuestionRepository : IGenericRepository<ClinicalQuestion>
{
    Task<IReadOnlyList<ClinicalQuestion>> GetActiveQuestionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClinicalQuestion>> GetQuestionsByChapterIdsAsync(
        IReadOnlyList<Guid> chapterIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClinicalQuestion>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<bool> IcdChapterExistsAsync(Guid icdChapterId, CancellationToken cancellationToken = default);

    Task UpdateChapterCodeByChapterIdAsync(
        Guid chapterId,
        string chapterCode,
        CancellationToken cancellationToken = default);
}

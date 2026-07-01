using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface ISymptomAnalysisSessionRepository : IGenericRepository<SymptomAnalysisSession>
{
    Task<PagedResult<SymptomAnalysisSession>> GetPagedByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> DetachChapterCodeAsync(
        string chapterCode,
        CancellationToken cancellationToken = default);

    Task AttachChapterCodeAsync(
        IReadOnlyList<Guid> ids,
        string chapterCode,
        CancellationToken cancellationToken = default);
}

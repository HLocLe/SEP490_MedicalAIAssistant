using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IMedicalDepartmentRepository : IGenericRepository<MedicalDepartment>
{
    Task<IReadOnlyList<MedicalDepartment>> GetActiveAsync(
        CancellationToken cancellationToken = default);

    Task<MedicalDepartment?> GetActiveByChapterCodeAsync(
        string chapterCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> DetachChapterCodeAsync(
        string chapterCode,
        CancellationToken cancellationToken = default);

    Task AttachChapterCodeAsync(
        IReadOnlyList<Guid> ids,
        string chapterCode,
        CancellationToken cancellationToken = default);
}

using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class MedicalDepartmentRepository
    : GenericRepository<MedicalDepartment>, IMedicalDepartmentRepository
{
    private readonly ApplicationDbContext _context;

    public MedicalDepartmentRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MedicalDepartment>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.MedicalDepartments
            .AsNoTracking()
            .Where(department => !department.IsDeleted)
            .OrderBy(department => department.DepartmentName ?? string.Empty)
            .ToListAsync(cancellationToken);
    }

    public async Task<MedicalDepartment?> GetActiveByChapterCodeAsync(
        string chapterCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chapterCode))
        {
            return null;
        }

        var normalizedChapterCode = chapterCode.Trim().ToUpperInvariant();

        return await _context.MedicalDepartments
            .AsNoTracking()
            .Where(department =>
                !department.IsDeleted
                && department.ChapterCode != null
                && department.ChapterCode.ToUpper().Contains(normalizedChapterCode))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> DetachChapterCodeAsync(
        string chapterCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chapterCode))
        {
            return Array.Empty<Guid>();
        }

        var ids = await _context.MedicalDepartments
            .Where(department => department.ChapterCode == chapterCode)
            .Select(department => department.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return ids;
        }

        var updatedAt = DateTime.UtcNow;
        await _context.MedicalDepartments
            .Where(department => ids.Contains(department.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(department => department.ChapterCode, (string?)null)
                    .SetProperty(department => department.UpdatedAt, updatedAt),
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
        await _context.MedicalDepartments
            .Where(department => ids.Contains(department.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(department => department.ChapterCode, chapterCode)
                    .SetProperty(department => department.UpdatedAt, updatedAt),
                cancellationToken);
    }
}

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
}

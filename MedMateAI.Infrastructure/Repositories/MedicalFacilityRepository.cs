using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class MedicalFacilityRepository
    : GenericRepository<MedicalFacility>, IMedicalFacilityRepository
{
    private readonly ApplicationDbContext _context;

    public MedicalFacilityRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<PagedResult<MedicalFacility>> GetPagedWithDepartmentsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : pageSize;
        normalizedPageSize = normalizedPageSize > 100 ? 100 : normalizedPageSize;

        var query = _context.MedicalFacilities
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        query = ApplySearch(query, search);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.FacilityDepartments.Where(fd => !fd.IsDeleted))
            .ThenInclude(x => x.Department)
            .OrderBy(x => (x.FacilityName ?? string.Empty).ToLower())
            .ThenBy(x => x.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new PagedResult<MedicalFacility>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items,
        };
    }

    public async Task<IReadOnlyList<MedicalFacility>> GetActiveWithDepartmentsAsync(
        Guid? departmentId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MedicalFacilities
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive);

        query = ApplyDepartmentFilter(query, departmentId);
        query = ApplySearch(query, search);

        return await query
            .Include(x => x.FacilityDepartments.Where(fd => !fd.IsDeleted))
            .ThenInclude(x => x.Department)
            .OrderBy(x => (x.FacilityName ?? string.Empty).ToLower())
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MedicalFacility>> GetActiveWithCoordinatesInBoundsAsync(
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        Guid? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MedicalFacilities
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.IsActive
                && x.Latitude.HasValue
                && x.Longitude.HasValue
                && x.Latitude >= (decimal)minLatitude
                && x.Latitude <= (decimal)maxLatitude
                && x.Longitude >= (decimal)minLongitude
                && x.Longitude <= (decimal)maxLongitude);

        query = ApplyDepartmentFilter(query, departmentId);

        return await query
            .Include(x => x.FacilityDepartments.Where(fd => !fd.IsDeleted))
            .ThenInclude(x => x.Department)
            .ToListAsync(cancellationToken);
    }

    public async Task<MedicalFacility?> GetByIdWithDepartmentsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.MedicalFacilities
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.FacilityDepartments.Where(fd => !fd.IsDeleted))
            .ThenInclude(x => x.Department)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FacilityDepartment>> GetFacilityDepartmentsAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default)
    {
        return await _context.FacilityDepartments
            .AsNoTracking()
            .Where(x => x.FacilityId == facilityId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FacilityDepartment>> GetActiveFacilityDepartmentsByDepartmentIdsAsync(
        IReadOnlyList<Guid> departmentIds,
        CancellationToken cancellationToken = default)
    {
        if (departmentIds.Count == 0)
        {
            return Array.Empty<FacilityDepartment>();
        }

        return await _context.FacilityDepartments
            .AsNoTracking()
            .Where(fd =>
                !fd.IsDeleted
                && departmentIds.Contains(fd.DepartmentId)
                && !fd.Facility.IsDeleted
                && fd.Facility.IsActive
                && !fd.Department.IsDeleted)
            .Include(fd => fd.Facility)
            .Include(fd => fd.Department)
            .OrderBy(fd => fd.Department.DepartmentName)
            .ThenBy(fd => fd.Facility.FacilityName)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<MedicalFacility> ApplyDepartmentFilter(
        IQueryable<MedicalFacility> query,
        Guid? departmentId)
    {
        if (!departmentId.HasValue || departmentId.Value == Guid.Empty)
        {
            return query;
        }

        return query.Where(x => x.FacilityDepartments.Any(fd =>
            !fd.IsDeleted
            && !fd.Department.IsDeleted
            && fd.DepartmentId == departmentId.Value));
    }

    private static IQueryable<MedicalFacility> ApplySearch(IQueryable<MedicalFacility> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var normalizedSearch = search.Trim().ToLower();
        return query.Where(x =>
            (x.FacilityName ?? string.Empty).ToLower().Contains(normalizedSearch)
            || (x.Address ?? string.Empty).ToLower().Contains(normalizedSearch));
    }
}

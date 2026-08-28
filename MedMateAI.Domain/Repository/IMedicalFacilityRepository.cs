using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IMedicalFacilityRepository
    : IGenericRepository<MedicalFacility>
{
    Task<PagedResult<MedicalFacility>> GetPagedWithDepartmentsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicalFacility>> GetActiveWithDepartmentsAsync(
        Guid? departmentId = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicalFacility>> GetActiveWithCoordinatesInBoundsAsync(
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        Guid? departmentId = null,
        CancellationToken cancellationToken = default);

    Task<MedicalFacility?> GetByIdWithDepartmentsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacilityDepartment>> GetFacilityDepartmentsAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacilityDepartment>> GetActiveFacilityDepartmentsByDepartmentIdsAsync(
        IReadOnlyList<Guid> departmentIds,
        CancellationToken cancellationToken = default);
}

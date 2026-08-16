using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Repository;

public interface IDoctorInvitationRepository : IGenericRepository<DoctorInvitation>
{
    Task<DoctorInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<DoctorInvitation?> GetPendingByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<DoctorInvitation?> GetPendingByDoctorIdAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<DoctorInvitation>> GetAdminPagedAsync(
        int pageNumber,
        int pageSize,
        DoctorInvitationStatus? status,
        string? search,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

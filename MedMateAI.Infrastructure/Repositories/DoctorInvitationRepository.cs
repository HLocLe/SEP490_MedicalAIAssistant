using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class DoctorInvitationRepository
    : GenericRepository<DoctorInvitation>, IDoctorInvitationRepository
{
    private readonly ApplicationDbContext _context;

    public DoctorInvitationRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<DoctorInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        return await _context.DoctorInvitations
            .Include(x => x.Doctor)
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash && !x.IsDeleted,
                cancellationToken);
    }

    public async Task<DoctorInvitation?> GetPendingByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var utcNow = DateTime.UtcNow;

        return await _context.DoctorInvitations
            .Where(x =>
                x.Email == normalizedEmail
                && x.Status == DoctorInvitationStatus.Pending
                && x.UsedAt == null
                && x.RevokedAt == null
                && x.ExpiresAt > utcNow
                && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DoctorInvitation?> GetPendingByDoctorIdAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        if (doctorId == Guid.Empty)
        {
            return null;
        }

        var utcNow = DateTime.UtcNow;

        return await _context.DoctorInvitations
            .Where(x =>
                x.DoctorId == doctorId
                && x.Status == DoctorInvitationStatus.Pending
                && x.UsedAt == null
                && x.RevokedAt == null
                && x.ExpiresAt > utcNow
                && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<DoctorInvitation>> GetAdminPagedAsync(
        int pageNumber,
        int pageSize,
        DoctorInvitationStatus? status,
        string? search,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 100);

        var query = _context.DoctorInvitations
            .AsNoTracking()
            .Where(invitation => !invitation.IsDeleted);

        query = ApplyEffectiveStatusFilter(query, status, utcNow);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(invitation =>
                invitation.Email.ToLower().Contains(normalizedSearch)
                || (invitation.Doctor != null
                    && (invitation.Doctor.FullName ?? string.Empty)
                        .ToLower()
                        .Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(invitation => invitation.Doctor)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ThenByDescending(invitation => invitation.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DoctorInvitation>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)normalizedPageSize),
            Items = items,
        };
    }

    private static IQueryable<DoctorInvitation> ApplyEffectiveStatusFilter(
        IQueryable<DoctorInvitation> query,
        DoctorInvitationStatus? status,
        DateTime utcNow)
    {
        return status switch
        {
            DoctorInvitationStatus.Pending => query.Where(invitation =>
                invitation.Status == DoctorInvitationStatus.Pending
                && invitation.UsedAt == null
                && invitation.RevokedAt == null
                && invitation.ExpiresAt > utcNow),

            DoctorInvitationStatus.Used => query.Where(invitation =>
                invitation.Status == DoctorInvitationStatus.Used
                || invitation.UsedAt != null),

            DoctorInvitationStatus.Revoked => query.Where(invitation =>
                invitation.Status != DoctorInvitationStatus.Used
                && invitation.UsedAt == null
                && (invitation.Status == DoctorInvitationStatus.Revoked
                    || invitation.RevokedAt != null)),

            DoctorInvitationStatus.Expired => query.Where(invitation =>
                invitation.UsedAt == null
                && invitation.RevokedAt == null
                && (invitation.Status == DoctorInvitationStatus.Expired
                    || (invitation.Status == DoctorInvitationStatus.Pending
                        && invitation.ExpiresAt <= utcNow))),

            _ => query,
        };
    }
}

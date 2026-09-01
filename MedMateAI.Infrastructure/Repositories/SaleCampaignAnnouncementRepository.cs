using System.Linq.Expressions;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class SaleCampaignAnnouncementRepository :
    ISaleCampaignAnnouncementRepository
{
    private const string PatientRoleName = "USER";
    private const string DoctorRoleName = "DOCTOR";
    private const string AdminRoleName = "ADMIN";

    private readonly ApplicationDbContext _context;

    public SaleCampaignAnnouncementRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SaleCampaignAnnouncementCampaignData>>
        GetAnnounceableCampaignPageAsync(
            DateTime utcNow,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        return await AnnounceableCampaigns(utcNow)
            .OrderBy(campaign => campaign.StartAt)
            .ThenBy(campaign => campaign.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(MapCampaign())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SaleCampaignAnnouncementCampaignData>>
        GetAnnounceableCampaignsAsync(
            IReadOnlyCollection<Guid> campaignIds,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
    {
        if (campaignIds.Count == 0)
        {
            return Array.Empty<SaleCampaignAnnouncementCampaignData>();
        }

        var normalizedIds = campaignIds.Distinct().ToArray();
        return await AnnounceableCampaigns(utcNow)
            .Where(campaign => normalizedIds.Contains(campaign.Id))
            .OrderBy(campaign => campaign.Id)
            .Select(MapCampaign())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SaleCampaignAnnouncementRecipientData>>
        GetPatientRecipientPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        return await EligiblePatients()
            .OrderBy(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(MapRecipient())
            .ToListAsync(cancellationToken);
    }

    public Task<SaleCampaignAnnouncementRecipientData?> GetPatientRecipientAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return EligiblePatients()
            .Where(user => user.Id == userId)
            .Select(MapRecipient())
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<SaleCampaign> AnnounceableCampaigns(
        DateTime utcNow)
    {
        return _context.SaleCampaigns
            .AsNoTracking()
            .Where(campaign =>
                !campaign.IsDeleted
                && campaign.AnnounceToUsers
                && campaign.IsActive
                && campaign.StartAt <= utcNow
                && campaign.EndAt > utcNow);
    }

    private IQueryable<ApplicationUser> EligiblePatients()
    {
        return _context.Users
            .AsNoTracking()
            .Where(user =>
                !user.IsDeleted
                && user.Status == UserStatus.Confirmed
                && (
                    from userRole in _context.UserRoles
                    join role in _context.Roles
                        on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                          && role.NormalizedName == PatientRoleName
                    select userRole.UserId).Any()
                && !(
                    from userRole in _context.UserRoles
                    join role in _context.Roles
                        on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                          && (role.NormalizedName == DoctorRoleName
                              || role.NormalizedName == AdminRoleName)
                    select userRole.UserId).Any());
    }

    private static Expression<
        Func<ApplicationUser, SaleCampaignAnnouncementRecipientData>>
        MapRecipient()
    {
        // Email confirmation controls this channel, not account or Sale eligibility.
        return user => new SaleCampaignAnnouncementRecipientData(
            user.Id,
            user.EmailConfirmed ? user.Email : null,
            user.DisplayName);
    }

    private static Expression<
        Func<SaleCampaign, SaleCampaignAnnouncementCampaignData>>
        MapCampaign()
    {
        return campaign => new SaleCampaignAnnouncementCampaignData(
            campaign.Id,
            campaign.Name,
            campaign.Description,
            campaign.BadgeText,
            campaign.EligibilityType,
            campaign.EndAt);
    }
}

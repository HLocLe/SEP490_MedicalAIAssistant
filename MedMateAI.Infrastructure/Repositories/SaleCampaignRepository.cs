using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class SaleCampaignRepository : ISaleCampaignRepository
{
    private readonly ApplicationDbContext _context;

    public SaleCampaignRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SaleCampaign>> GetAdminPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 100);
        var baseQuery = _context.SaleCampaigns
            .AsNoTracking()
            .Where(campaign => !campaign.IsDeleted);
        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .Include(campaign => campaign.CampaignPlans.Where(plan => !plan.IsDeleted))
            .ThenInclude(campaignPlan => campaignPlan.Plan)
            .Include(campaign => campaign.Redemptions.Where(redemption => !redemption.IsDeleted))
            .AsSplitQuery()
            .OrderByDescending(campaign => campaign.CreatedAt)
            .ThenByDescending(campaign => campaign.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SaleCampaign>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)normalizedPageSize),
            Items = items
        };
    }

    public Task<SaleCampaign?> GetByIdWithDetailsAsync(
        Guid campaignId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SaleCampaign> query = _context.SaleCampaigns;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query
            .Include(campaign => campaign.CampaignPlans.Where(plan => !plan.IsDeleted))
            .ThenInclude(campaignPlan => campaignPlan.Plan)
            .Include(campaign => campaign.Redemptions.Where(redemption => !redemption.IsDeleted))
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                campaign => campaign.Id == campaignId && !campaign.IsDeleted,
                cancellationToken);
    }

    public async Task<SaleCampaign?> GetByIdForUpdateAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();
        var campaigns = await _context.SaleCampaigns
            .FromSqlInterpolated($"""
                SELECT *
                FROM "SaleCampaign"
                WHERE "SaleCampaignId" = {campaignId}
                  AND "IsDeleted" = FALSE
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);
        var campaign = campaigns.SingleOrDefault();
        if (campaign is null)
        {
            return null;
        }

        await _context.Entry(campaign)
            .Collection(current => current.CampaignPlans)
            .Query()
            .Include(campaignPlan => campaignPlan.Plan)
            .LoadAsync(cancellationToken);
        return campaign;
    }

    public async Task<IReadOnlyList<SaleCampaignPlan>> GetOfferCandidatesAsync(
        IReadOnlyCollection<Guid> planIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (planIds.Count == 0)
        {
            return Array.Empty<SaleCampaignPlan>();
        }

        var normalizedPlanIds = planIds.Distinct().ToArray();
        return await _context.SaleCampaignPlans
            .AsNoTracking()
            .Include(campaignPlan => campaignPlan.SaleCampaign)
            .Include(campaignPlan => campaignPlan.Plan)
            .Where(campaignPlan =>
                normalizedPlanIds.Contains(campaignPlan.PlanId)
                && !campaignPlan.IsDeleted
                && campaignPlan.IsActive
                && !campaignPlan.SaleCampaign.IsDeleted
                && campaignPlan.SaleCampaign.IsActive
                && campaignPlan.SaleCampaign.StartAt <= utcNow
                && campaignPlan.SaleCampaign.EndAt > utcNow)
            .OrderByDescending(campaignPlan => campaignPlan.SaleCampaign.Priority)
            .ThenBy(campaignPlan => campaignPlan.SaleCampaign.EndAt)
            .ThenBy(campaignPlan => campaignPlan.SaleCampaign.CreatedAt)
            .ThenBy(campaignPlan => campaignPlan.SaleCampaign.Id)
            .ThenBy(campaignPlan => campaignPlan.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<SaleCampaignPlan?> GetCampaignPlanAsync(
        Guid campaignId,
        Guid planId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SaleCampaignPlan> query = _context.SaleCampaignPlans;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query
            .Include(campaignPlan => campaignPlan.Plan)
            .SingleOrDefaultAsync(
                campaignPlan =>
                    campaignPlan.SaleCampaignId == campaignId
                    && campaignPlan.PlanId == planId
                    && !campaignPlan.IsDeleted,
                cancellationToken);
    }

    public void Add(SaleCampaign campaign)
    {
        _context.SaleCampaigns.Add(campaign);
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Sale campaign locking requires an active database transaction.");
        }
    }
}

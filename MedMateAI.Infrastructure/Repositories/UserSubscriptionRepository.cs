using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class UserSubscriptionRepository
    : GenericRepository<UserSubscription>, IUserSubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public UserSubscriptionRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<UserSubscription?> GetByIdWithPlanAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _context.UserSubscriptions
            .AsNoTracking()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<UserSubscription?> GetCurrentActiveByUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        return await _context.UserSubscriptions
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId
                && !x.IsDeleted
                && x.Status == SubscriptionStatus.Active
                && x.StartDate.HasValue
                && x.StartDate.Value <= utcNow
                && (!x.EndDate.HasValue || x.EndDate.Value > utcNow))
            .OrderBy(x => x.EndDate.HasValue ? 0 : 1)
            .ThenBy(x => x.EndDate)
            .ThenBy(x => x.StartDate)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSubscription>> GetByUserWithPlanAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<UserSubscription>();
        }

        return await _context.UserSubscriptions
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .Include(x => x.Plan)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<UserSubscription?> GetCurrentActiveWithPlanQuotasAsync(
        Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return _context.UserSubscriptions.AsNoTracking()
            .Include(x => x.Plan)
                .ThenInclude(x => x.SubscriptionPlanQuotas)
                .ThenInclude(x => x.Quota)
            .Where(x => x.UserId == userId && !x.IsDeleted
                && x.Status == SubscriptionStatus.Active
                && x.StartDate.HasValue
                && x.StartDate.Value <= utcNow
                && (!x.EndDate.HasValue || x.EndDate.Value > utcNow)
                && !x.Plan.IsDeleted)
            .OrderBy(x => x.EndDate.HasValue ? 0 : 1)
            .ThenBy(x => x.EndDate)
            .ThenBy(x => x.StartDate)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<UserSubscription>> GetAdminPagedAsync(
        int pageNumber,
        int pageSize,
        SubscriptionStatus? status,
        bool currentOnly,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 100);

        var query = _context.UserSubscriptions
            .AsNoTracking()
            .Where(subscription => !subscription.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(subscription => subscription.Status == status.Value);
        }

        int totalCount;
        if (currentOnly)
        {
            query = query.Where(subscription =>
                subscription.Status == SubscriptionStatus.Active
                && subscription.StartDate.HasValue
                && subscription.StartDate.Value <= utcNow
                && (!subscription.EndDate.HasValue || subscription.EndDate.Value > utcNow));

            totalCount = await query
                .Select(subscription => subscription.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            var eligibleQuery = query;
            query = eligibleQuery.Where(subscription =>
                subscription.Id == eligibleQuery
                    .Where(candidate => candidate.UserId == subscription.UserId)
                    .OrderByDescending(candidate => candidate.StartDate)
                    .ThenByDescending(candidate => candidate.CreatedAt)
                    .ThenByDescending(candidate => candidate.Id)
                    .Select(candidate => candidate.Id)
                    .First());
        }
        else
        {
            totalCount = await query.CountAsync(cancellationToken);
        }

        var items = await query
            .Include(subscription => subscription.Plan)
            .OrderByDescending(subscription => subscription.StartDate)
            .ThenByDescending(subscription => subscription.CreatedAt)
            .ThenByDescending(subscription => subscription.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSubscription>
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
}

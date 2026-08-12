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
}

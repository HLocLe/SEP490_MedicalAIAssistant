using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class SubscriptionPlanQuotaRepository
    : ISubscriptionPlanQuotaRepository
{
    private readonly ApplicationDbContext _context;

    public SubscriptionPlanQuotaRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Quota>> ListQuotaDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Quotas
            .AsNoTracking()
            .Where(quota => !quota.IsDeleted)
            .OrderBy(quota => quota.Code)
            .ThenBy(quota => quota.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Quota?> GetQuotaDefinitionAsync(
        Guid quotaId,
        CancellationToken cancellationToken = default)
    {
        return _context.Quotas
            .AsNoTracking()
            .SingleOrDefaultAsync(
                quota => quota.Id == quotaId && !quota.IsDeleted,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlanQuota>> ListPlanQuotasAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SubscriptionPlanQuotas
            .AsNoTracking()
            .Include(mapping => mapping.Quota)
            .Where(mapping => mapping.PlanId == planId && !mapping.IsDeleted)
            .OrderBy(mapping => mapping.Quota.Code)
            .ThenBy(mapping => mapping.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlanQuota>> ListActivePlanQuotasAsync(
        IReadOnlyCollection<Guid> planIds,
        CancellationToken cancellationToken = default)
    {
        if (planIds.Count == 0)
        {
            return Array.Empty<SubscriptionPlanQuota>();
        }

        var normalizedPlanIds = planIds.Distinct().ToArray();

        return await _context.SubscriptionPlanQuotas
            .AsNoTracking()
            .Include(mapping => mapping.Quota)
            .Where(mapping =>
                normalizedPlanIds.Contains(mapping.PlanId)
                && !mapping.IsDeleted
                && mapping.IsActive
                && !mapping.Quota.IsDeleted
                && mapping.Quota.IsActive)
            .OrderBy(mapping => mapping.PlanId)
            .ThenBy(mapping => mapping.Quota.Code)
            .ThenBy(mapping => mapping.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionPlan?> GetPlanForUpdateAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        var plans = await _context.SubscriptionPlans
            .FromSqlInterpolated($"""
                SELECT *
                FROM "SubscriptionPlan"
                WHERE "PlanId" = {planId}
                  AND "IsDeleted" = FALSE
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        return plans.SingleOrDefault();
    }

    public Task<SubscriptionPlanQuota?> GetNonDeletedMappingAsync(
        Guid planId,
        Guid quotaId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        return _context.SubscriptionPlanQuotas
            .AsTracking()
            .SingleOrDefaultAsync(
                mapping =>
                    mapping.PlanId == planId
                    && mapping.QuotaId == quotaId
                    && !mapping.IsDeleted,
                cancellationToken);
    }

    public Task<SubscriptionPlanQuota?> GetLatestDeletedMappingAsync(
        Guid planId,
        Guid quotaId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        return _context.SubscriptionPlanQuotas
            .AsTracking()
            .Where(mapping =>
                mapping.PlanId == planId
                && mapping.QuotaId == quotaId
                && mapping.IsDeleted)
            .OrderByDescending(mapping =>
                mapping.DeletedAt
                ?? mapping.UpdatedAt
                ?? mapping.CreatedAt)
            .ThenByDescending(mapping => mapping.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(SubscriptionPlanQuota mapping)
    {
        _context.SubscriptionPlanQuotas.Add(mapping);
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Subscription plan quota writes require an active database transaction.");
        }
    }
}

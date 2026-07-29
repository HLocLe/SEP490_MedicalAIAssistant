using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class QuotaUsageRepository : IQuotaUsageRepository
{
    private readonly ApplicationDbContext _context;

    public QuotaUsageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserSubscriptionUsage> GetOrCreateAsync(
        Guid subscriptionId,
        Guid quotaId,
        DateTime cycleStart,
        DateTime cycleEnd,
        int limitValue,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        var usageId = Guid.NewGuid();

        // Raw SQL keeps get-or-create race-safe without a check-then-insert window.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "UserSubscriptionUsage"
                ("UserSubscriptionUsageId", "UserSubscriptionId", "QuotaId", "LimitValue",
                 "UsedCount", "ReservedCount", "CycleStart", "CycleEnd", "Version",
                 "CreatedAt", "UpdatedAt", "IsDeleted")
            VALUES
                ({usageId}, {subscriptionId}, {quotaId}, {limitValue},
                 0, 0, {cycleStart}, {cycleEnd}, 0,
                 {utcNow}, NULL, false)
            ON CONFLICT ("UserSubscriptionId", "QuotaId", "CycleStart", "CycleEnd")
                WHERE "IsDeleted" = false
            DO NOTHING;
            """,
            cancellationToken);

        var usage = await _context.UserSubscriptionUsages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                currentUsage =>
                    currentUsage.UserSubscriptionId == subscriptionId
                    && currentUsage.QuotaId == quotaId
                    && currentUsage.CycleStart == cycleStart
                    && currentUsage.CycleEnd == cycleEnd
                    && !currentUsage.IsDeleted,
                cancellationToken);

        if (usage is not null)
        {
            return usage;
        }

        throw new InvalidOperationException(
            "The conflict-safe subscription usage insert completed, but the usage row could not be loaded.");
    }

    public async Task<QuotaMutationResult?> ReserveAsync(
        Guid usageId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        var affectedRows = await _context.UserSubscriptionUsages
            .Where(usage =>
                usage.Id == usageId
                && !usage.IsDeleted
                && usage.UsedCount + usage.ReservedCount < usage.LimitValue)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(usage => usage.ReservedCount, usage => usage.ReservedCount + 1)
                    .SetProperty(usage => usage.Version, usage => usage.Version + 1)
                    .SetProperty(usage => usage.UpdatedAt, utcNow),
                cancellationToken);

        if (affectedRows == 0)
        {
            return null;
        }

        var state = await GetMutationStateAsync(usageId, cancellationToken);

        return new QuotaMutationResult(
            state.UsageId,
            state.UserSubscriptionId,
            state.QuotaId,
            state.LimitValue,
            state.UsedCount,
            state.UsedCount,
            state.ReservedCount - 1,
            state.ReservedCount);
    }

    public async Task<QuotaMutationResult?> ReleaseAsync(
        Guid usageId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        var affectedRows = await _context.UserSubscriptionUsages
            .Where(usage =>
                usage.Id == usageId
                && !usage.IsDeleted
                && usage.ReservedCount > 0)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(usage => usage.ReservedCount, usage => usage.ReservedCount - 1)
                    .SetProperty(usage => usage.Version, usage => usage.Version + 1)
                    .SetProperty(usage => usage.UpdatedAt, utcNow),
                cancellationToken);

        if (affectedRows == 0)
        {
            return null;
        }

        var state = await GetMutationStateAsync(usageId, cancellationToken);

        return new QuotaMutationResult(
            state.UsageId,
            state.UserSubscriptionId,
            state.QuotaId,
            state.LimitValue,
            state.UsedCount,
            state.UsedCount,
            state.ReservedCount + 1,
            state.ReservedCount);
    }

    public async Task<QuotaMutationResult?> ConsumeAsync(
        Guid usageId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        var affectedRows = await _context.UserSubscriptionUsages
            .Where(usage =>
                usage.Id == usageId
                && !usage.IsDeleted
                && usage.ReservedCount > 0
                && usage.UsedCount < usage.LimitValue)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(usage => usage.ReservedCount, usage => usage.ReservedCount - 1)
                    .SetProperty(usage => usage.UsedCount, usage => usage.UsedCount + 1)
                    .SetProperty(usage => usage.Version, usage => usage.Version + 1)
                    .SetProperty(usage => usage.UpdatedAt, utcNow),
                cancellationToken);

        if (affectedRows == 0)
        {
            return null;
        }

        var state = await GetMutationStateAsync(usageId, cancellationToken);

        return new QuotaMutationResult(
            state.UsageId,
            state.UserSubscriptionId,
            state.QuotaId,
            state.LimitValue,
            state.UsedCount - 1,
            state.UsedCount,
            state.ReservedCount + 1,
            state.ReservedCount);
    }

    public async Task<QuotaMutationResult?> RestoreAsync(
        Guid usageId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        var affectedRows = await _context.UserSubscriptionUsages
            .Where(usage =>
                usage.Id == usageId
                && !usage.IsDeleted
                && usage.UsedCount > 0)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(usage => usage.UsedCount, usage => usage.UsedCount - 1)
                    .SetProperty(usage => usage.Version, usage => usage.Version + 1)
                    .SetProperty(usage => usage.UpdatedAt, utcNow),
                cancellationToken);

        if (affectedRows == 0)
        {
            return null;
        }

        var state = await GetMutationStateAsync(usageId, cancellationToken);

        return new QuotaMutationResult(
            state.UsageId,
            state.UserSubscriptionId,
            state.QuotaId,
            state.LimitValue,
            state.UsedCount + 1,
            state.UsedCount,
            state.ReservedCount,
            state.ReservedCount);
    }

    public Task<UserSubscriptionLog?> GetLogByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return _context.UserSubscriptionLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                log => log.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public async Task<bool> TryInsertLogAsync(UserSubscriptionLog log, CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        // Raw SQL preserves the unique-key replay behavior without a check-then-insert race.
        var actionType = log.ActionType.ToString();
        var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "UserSubscriptionLog"
                ("UserSubscriptionLogId", "UserSubscriptionId", "UserSubscriptionUsageId", "QuotaId",
                 "ActionType", "Quantity", "UsedCountBefore", "UsedCountAfter", "ReservedCountBefore",
                 "ReservedCountAfter", "ReferenceType", "ReferenceId", "Reason", "IdempotencyKey",
                 "PerformedByUserId", "CreatedAt")
            VALUES
                ({log.Id}, {log.UserSubscriptionId}, {log.UserSubscriptionUsageId}, {log.QuotaId},
                 {actionType}, {log.Quantity}, {log.UsedCountBefore}, {log.UsedCountAfter},
                 {log.ReservedCountBefore}, {log.ReservedCountAfter}, {log.ReferenceType},
                 {log.ReferenceId}, {log.Reason}, {log.IdempotencyKey}, {log.PerformedByUserId},
                 {log.CreatedAt})
            ON CONFLICT ("IdempotencyKey")
                WHERE "IdempotencyKey" IS NOT NULL
            DO NOTHING;
            """,
            cancellationToken);

        return affectedRows == 1;
    }

    public async Task<IReadOnlyList<UserSubscriptionUsage>> GetBySubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserSubscriptionUsages
            .AsNoTracking()
            .Where(usage =>
                usage.UserSubscriptionId == subscriptionId
                && !usage.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public Task<UserSubscriptionUsage?> GetByIdAsync(
        Guid usageId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserSubscriptionUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                usage => usage.Id == usageId && !usage.IsDeleted,
                cancellationToken);
    }

    public Task<UserSubscriptionUsage?> GetByIdForQuotaAsync(
        Guid usageId,
        Guid userSubscriptionId,
        string quotaCode,
        CancellationToken cancellationToken = default)
    {
        return _context.UserSubscriptionUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                usage =>
                    usage.Id == usageId
                    && !usage.IsDeleted
                    && usage.UserSubscriptionId == userSubscriptionId
                    && !usage.Quota.IsDeleted
                    && usage.Quota.IsActive
                    && usage.Quota.Code == quotaCode,
                cancellationToken);
    }

    private async Task<QuotaMutationState> GetMutationStateAsync(
        Guid usageId,
        CancellationToken cancellationToken)
    {
        var state = await _context.UserSubscriptionUsages
            .AsNoTracking()
            .Where(usage => usage.Id == usageId && !usage.IsDeleted)
            .Select(usage => new QuotaMutationState(
                usage.Id,
                usage.UserSubscriptionId,
                usage.QuotaId,
                usage.LimitValue,
                usage.UsedCount,
                usage.ReservedCount))
            .SingleOrDefaultAsync(cancellationToken);

        if (state is not null)
        {
            return state;
        }

        throw new InvalidOperationException(
            $"Quota usage '{usageId}' was updated, but its mutation state could not be loaded.");
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Quota usage writes require an active database transaction.");
        }
    }

    private sealed record QuotaMutationState(
        Guid UsageId,
        Guid UserSubscriptionId,
        Guid QuotaId,
        int LimitValue,
        int UsedCount,
        int ReservedCount);
}

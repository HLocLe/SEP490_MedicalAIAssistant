using MedMateAI.Application.Common;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly ApplicationDbContext _context;

    public OutboxMessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OutboxProcessingItem>> ClaimBatchAsync(
        DateTime utcNow,
        int batchSize,
        TimeSpan processingLease,
        CancellationToken cancellationToken = default)
    {
        var pendingStatus = OutboxMessageStatus.Pending.ToString();
        var processingStatus = OutboxMessageStatus.Processing.ToString();
        var recoveryPlanAggregate =
            RecoveryPlanLifecycleOutboxEventTypes.AggregateType;
        var recoveryPlanRequestAggregate =
            RecoveryPlanOutboxEventTypes.AggregateType;
        var leaseExpiresAtUtc = utcNow.Add(processingLease);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // SKIP LOCKED lets multiple instances claim disjoint batches without blocking.
            var messages = await _context.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "OutboxMessage"
                    WHERE "AggregateType" IN (
                        {recoveryPlanAggregate},
                        {recoveryPlanRequestAggregate})
                      AND (
                        ("Status" = {pendingStatus}
                         AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {utcNow}))
                        OR
                        ("Status" = {processingStatus}
                         AND "NextAttemptAt" IS NOT NULL
                         AND "NextAttemptAt" <= {utcNow})
                    )
                    ORDER BY
                        ("NextAttemptAt" IS NOT NULL),
                        "CreatedAt",
                        "OutboxMessageId"
                    FOR UPDATE SKIP LOCKED
                    LIMIT {batchSize}
                    """)
                .AsTracking()
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                message.Status = OutboxMessageStatus.Processing;
                message.AttemptCount++;
                message.NextAttemptAt = leaseExpiresAtUtc;
                message.LastError = null;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var claimedItems = messages
                .Select(message => new OutboxProcessingItem(
                    message.Id,
                    message.EventType,
                    message.AggregateType,
                    message.AggregateId,
                    message.AttemptCount))
                .ToList();

            _context.ChangeTracker.Clear();
            return claimedItems;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<RecoveryPlanNotificationData?> GetRecoveryPlanNotificationDataAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        return (
            from plan in _context.RecoveryPlans.AsNoTracking()
            join user in _context.Users.AsNoTracking() on plan.UserId equals user.Id
            where plan.Id == planId && !plan.IsDeleted
            select new RecoveryPlanNotificationData(
                plan.Id,
                plan.UserId,
                plan.Status,
                !user.IsDeleted && user.Status == UserStatus.Confirmed))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> RenewLeaseAsync(
        Guid outboxMessageId,
        int attemptCount,
        DateTime leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await ProcessingAttempt(outboxMessageId, attemptCount)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    message => message.NextAttemptAt,
                    leaseExpiresAtUtc),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkProcessedAsync(
        Guid outboxMessageId,
        int attemptCount,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await ProcessingAttempt(outboxMessageId, attemptCount)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.Status, OutboxMessageStatus.Processed)
                    .SetProperty(message => message.ProcessedAt, utcNow)
                    .SetProperty(message => message.NextAttemptAt, (DateTime?)null)
                    .SetProperty(message => message.LastError, (string?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> ScheduleRetryAsync(
        Guid outboxMessageId,
        int attemptCount,
        DateTime retryAtUtc,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await ProcessingAttempt(outboxMessageId, attemptCount)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.Status, OutboxMessageStatus.Pending)
                    .SetProperty(message => message.ProcessedAt, (DateTime?)null)
                    .SetProperty(message => message.NextAttemptAt, retryAtUtc)
                    .SetProperty(message => message.LastError, lastError),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkFailedAsync(
        Guid outboxMessageId,
        int attemptCount,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await ProcessingAttempt(outboxMessageId, attemptCount)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.Status, OutboxMessageStatus.Failed)
                    .SetProperty(message => message.ProcessedAt, (DateTime?)null)
                    .SetProperty(message => message.NextAttemptAt, (DateTime?)null)
                    .SetProperty(message => message.LastError, lastError),
                cancellationToken);

        return affectedRows == 1;
    }

    private IQueryable<Domain.Entities.OutboxMessage> ProcessingAttempt(
        Guid outboxMessageId,
        int attemptCount)
    {
        return _context.OutboxMessages.Where(message =>
            message.Id == outboxMessageId
            && message.Status == OutboxMessageStatus.Processing
            && message.AttemptCount == attemptCount);
    }
}

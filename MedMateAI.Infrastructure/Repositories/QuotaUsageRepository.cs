using System.Data.Common;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class QuotaUsageRepository : IQuotaUsageRepository
{
    private readonly ApplicationDbContext _context;

    public QuotaUsageRepository(ApplicationDbContext context) => _context = context;

    public async Task<UserSubscriptionUsage> GetOrCreateAsync(
        Guid subscriptionId, Guid quotaId, DateTime cycleStart, DateTime cycleEnd,
        int limitValue, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        const string insertSql = """
            INSERT INTO "UserSubscriptionUsage"
                ("UserSubscriptionUsageId","UserSubscriptionId","QuotaId","LimitValue",
                 "UsedCount","ReservedCount","CycleStart","CycleEnd","Version",
                 "CreatedAt","UpdatedAt","IsDeleted")
            VALUES (@id,@subscriptionId,@quotaId,@limitValue,0,0,@cycleStart,@cycleEnd,0,@now,NULL,false)
            ON CONFLICT ("UserSubscriptionId","QuotaId","CycleStart","CycleEnd")
                WHERE "IsDeleted" = false DO NOTHING
            RETURNING "UserSubscriptionUsageId","UserSubscriptionId","QuotaId","LimitValue",
                      "UsedCount","ReservedCount","CycleStart","CycleEnd","Version","CreatedAt";
            """;
        const string selectSql = """
            SELECT "UserSubscriptionUsageId","UserSubscriptionId","QuotaId","LimitValue",
                   "UsedCount","ReservedCount","CycleStart","CycleEnd","Version","CreatedAt"
            FROM "UserSubscriptionUsage"
            WHERE "UserSubscriptionId"=@subscriptionId AND "QuotaId"=@quotaId
              AND "CycleStart"=@cycleStart AND "CycleEnd"=@cycleEnd AND "IsDeleted"=false
            LIMIT 1;
            """;

        await using (var insertCommand = CreateTransactionalCommand(insertSql))
        {
            AddUsageKeyParameters(insertCommand, subscriptionId, quotaId, cycleStart, cycleEnd);
            Add(insertCommand, "id", Guid.NewGuid());
            Add(insertCommand, "limitValue", limitValue);
            Add(insertCommand, "now", utcNow);

            await using var insertReader = await insertCommand.ExecuteReaderAsync(cancellationToken);
            if (await insertReader.ReadAsync(cancellationToken))
            {
                return ReadUsage(insertReader);
            }
        }

        await using var selectCommand = CreateTransactionalCommand(selectSql);
        AddUsageKeyParameters(selectCommand, subscriptionId, quotaId, cycleStart, cycleEnd);
        await using var selectReader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        if (await selectReader.ReadAsync(cancellationToken))
        {
            return ReadUsage(selectReader);
        }

        throw new InvalidOperationException(
            "Subscription usage was not returned by the conflict-safe insert and could not be loaded afterward.");
    }

    public Task<QuotaMutationResult?> ReserveAsync(Guid usageId, DateTime now, CancellationToken token = default) =>
        MutateAsync(usageId, now, "AND \"UsedCount\" + \"ReservedCount\" < \"LimitValue\"",
            "\"ReservedCount\" = \"ReservedCount\" + 1", 0, -1, token);

    public Task<QuotaMutationResult?> ReleaseAsync(Guid usageId, DateTime now, CancellationToken token = default) =>
        MutateAsync(usageId, now, "AND \"ReservedCount\" > 0",
            "\"ReservedCount\" = \"ReservedCount\" - 1", 0, 1, token);

    public Task<QuotaMutationResult?> ConsumeAsync(Guid usageId, DateTime now, CancellationToken token = default) =>
        MutateAsync(usageId, now, "AND \"ReservedCount\" > 0 AND \"UsedCount\" < \"LimitValue\"",
            "\"ReservedCount\" = \"ReservedCount\" - 1, \"UsedCount\" = \"UsedCount\" + 1", -1, 1, token);

    public Task<QuotaMutationResult?> RestoreAsync(Guid usageId, DateTime now, CancellationToken token = default) =>
        MutateAsync(usageId, now, "AND \"UsedCount\" > 0",
            "\"UsedCount\" = \"UsedCount\" - 1", 1, 0, token);

    public Task<UserSubscriptionLog?> GetLogByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default) =>
        _context.UserSubscriptionLogs.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);

    public async Task<bool> TryInsertLogAsync(UserSubscriptionLog log, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "UserSubscriptionLog"
                ("UserSubscriptionLogId","UserSubscriptionId","UserSubscriptionUsageId","QuotaId",
                 "ActionType","Quantity","UsedCountBefore","UsedCountAfter","ReservedCountBefore",
                 "ReservedCountAfter","ReferenceType","ReferenceId","Reason","IdempotencyKey",
                 "PerformedByUserId","CreatedAt")
            VALUES (@id,@subscriptionId,@usageId,@quotaId,@actionType,@quantity,@usedBefore,@usedAfter,
                    @reservedBefore,@reservedAfter,@referenceType,@referenceId,@reason,@key,@actor,@createdAt)
            ON CONFLICT ("IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL DO NOTHING;
            """;
        await using var command = CreateTransactionalCommand(sql);
        Add(command, "id", log.Id);
        Add(command, "subscriptionId", log.UserSubscriptionId);
        Add(command, "usageId", log.UserSubscriptionUsageId);
        Add(command, "quotaId", log.QuotaId);
        Add(command, "actionType", log.ActionType.ToString());
        Add(command, "quantity", log.Quantity);
        Add(command, "usedBefore", log.UsedCountBefore);
        Add(command, "usedAfter", log.UsedCountAfter);
        Add(command, "reservedBefore", log.ReservedCountBefore);
        Add(command, "reservedAfter", log.ReservedCountAfter);
        Add(command, "referenceType", log.ReferenceType);
        Add(command, "referenceId", log.ReferenceId);
        Add(command, "reason", log.Reason);
        Add(command, "key", log.IdempotencyKey);
        Add(command, "actor", log.PerformedByUserId);
        Add(command, "createdAt", log.CreatedAt);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<UserSubscriptionUsage>> GetBySubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default) =>
        await _context.UserSubscriptionUsages.AsNoTracking()
            .Where(x => x.UserSubscriptionId == subscriptionId && !x.IsDeleted).ToListAsync(cancellationToken);

    public Task<UserSubscriptionUsage?> GetByIdAsync(Guid usageId, CancellationToken cancellationToken = default) =>
        _context.UserSubscriptionUsages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == usageId && !x.IsDeleted, cancellationToken);

    private async Task<QuotaMutationResult?> MutateAsync(
        Guid usageId, DateTime now, string condition, string assignments,
        int usedBeforeDelta, int reservedBeforeDelta, CancellationToken token)
    {
        var sql = $"""
            UPDATE "UserSubscriptionUsage"
            SET {assignments}, "Version"="Version"+1, "UpdatedAt"=@now
            WHERE "UserSubscriptionUsageId"=@usageId AND "IsDeleted"=false {condition}
            RETURNING "UserSubscriptionUsageId","UserSubscriptionId","QuotaId","LimitValue","UsedCount","ReservedCount";
            """;
        await using var command = CreateTransactionalCommand(sql);
        Add(command, "usageId", usageId);
        Add(command, "now", now);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        var usedAfter = reader.GetInt32(4);
        var reservedAfter = reader.GetInt32(5);
        return new QuotaMutationResult(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
            reader.GetInt32(3), usedAfter + usedBeforeDelta, usedAfter,
            reservedAfter + reservedBeforeDelta, reservedAfter);
    }

    private DbCommand CreateTransactionalCommand(string sql)
    {
        var transaction = _context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Quota mutation requires an active database transaction.");
        var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction.GetDbTransaction();
        return command;
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void AddUsageKeyParameters(
        DbCommand command, Guid subscriptionId, Guid quotaId, DateTime cycleStart, DateTime cycleEnd)
    {
        Add(command, "subscriptionId", subscriptionId);
        Add(command, "quotaId", quotaId);
        Add(command, "cycleStart", cycleStart);
        Add(command, "cycleEnd", cycleEnd);
    }

    private static UserSubscriptionUsage ReadUsage(DbDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        UserSubscriptionId = reader.GetGuid(1),
        QuotaId = reader.GetGuid(2),
        LimitValue = reader.GetInt32(3),
        UsedCount = reader.GetInt32(4),
        ReservedCount = reader.GetInt32(5),
        CycleStart = reader.GetDateTime(6),
        CycleEnd = reader.GetDateTime(7),
        Version = reader.GetInt32(8),
        CreatedAt = reader.GetDateTime(9)
    };
}

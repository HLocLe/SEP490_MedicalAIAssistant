using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class UserPushDeviceRepository : IUserPushDeviceRepository
{
    private readonly ApplicationDbContext _context;

    public UserPushDeviceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserPushDeviceRegistrationResult> RegisterOrUpdateAsync(
        Guid userId,
        string installationId,
        string expoPushToken,
        string platform,
        string? appVersion,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // These transaction-scoped database locks serialize ownership changes
            // across every application instance without retaining the push token.
            await AcquireRegistrationLockAsync(
                $"push-token:{expoPushToken}",
                cancellationToken);
            await AcquireRegistrationLockAsync(
                $"push-installation:{installationId}",
                cancellationToken);

            var userExists = await _context.Users
                .AnyAsync(
                    user => user.Id == userId && !user.IsDeleted,
                    cancellationToken);
            if (!userExists)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new UserPushDeviceRegistrationResult(
                    UserPushDeviceRegistrationStatus.UserNotFound);
            }

            await _context.UserPushDevices
                .Where(device =>
                    !device.IsDeleted
                    && device.IsActive
                    && (
                        (device.ExpoPushToken == expoPushToken
                         && (device.UserId != userId
                             || device.InstallationId != installationId))
                        ||
                        (device.InstallationId == installationId
                         && device.UserId != userId)))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(device => device.IsActive, false)
                        .SetProperty(device => device.UpdatedAt, utcNow),
                    cancellationToken);

            var device = await _context.UserPushDevices
                .SingleOrDefaultAsync(
                    current =>
                        current.UserId == userId
                        && current.InstallationId == installationId
                        && !current.IsDeleted,
                    cancellationToken);

            if (device is null)
            {
                device = new UserPushDevice
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    InstallationId = installationId,
                    ExpoPushToken = expoPushToken,
                    TokenVersion = 1,
                    Platform = platform,
                    AppVersion = appVersion,
                    IsActive = true,
                    LastSeenAt = utcNow,
                    CreatedAt = utcNow
                };
                _context.UserPushDevices.Add(device);
            }
            else
            {
                if (!string.Equals(
                        device.ExpoPushToken,
                        expoPushToken,
                        StringComparison.Ordinal))
                {
                    device.TokenVersion++;
                }

                device.ExpoPushToken = expoPushToken;
                device.Platform = platform;
                device.AppVersion = appVersion;
                device.IsActive = true;
                device.LastSeenAt = utcNow;
                device.UpdatedAt = utcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new UserPushDeviceRegistrationResult(
                UserPushDeviceRegistrationStatus.Success,
                new UserPushDeviceRegistrationData(
                    device.Id,
                    device.InstallationId,
                    device.Platform,
                    device.IsActive,
                    device.LastSeenAt));
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException
                  && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            return new UserPushDeviceRegistrationResult(
                UserPushDeviceRegistrationStatus.Conflict);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<bool> DeactivateByInstallationAsync(
        Guid userId,
        string installationId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.UserPushDevices
            .Where(device =>
                device.UserId == userId
                && device.InstallationId == installationId
                && !device.IsDeleted
                && device.IsActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(device => device.IsActive, false)
                    .SetProperty(device => device.UpdatedAt, utcNow),
                cancellationToken);

        return affectedRows > 0;
    }

    public async Task<bool> DeactivateIfTokenVersionMatchesAsync(
        Guid deviceId,
        Guid expectedUserId,
        int expectedTokenVersion,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.UserPushDevices
            .Where(device =>
                device.Id == deviceId
                && device.UserId == expectedUserId
                && device.TokenVersion == expectedTokenVersion
                && !device.IsDeleted
                && device.IsActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(device => device.IsActive, false)
                    .SetProperty(device => device.UpdatedAt, utcNow),
                cancellationToken);

        return affectedRows > 0;
    }

    public async Task<IReadOnlyList<UserPushDeviceData>> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserPushDevices
            .AsNoTracking()
            .Where(device =>
                device.UserId == userId
                && device.IsActive
                && !device.IsDeleted)
            .OrderBy(device => device.Id)
            .Select(device => new UserPushDeviceData(
                device.Id,
                device.UserId,
                device.ExpoPushToken,
                device.TokenVersion,
                device.Platform,
                device.IsActive))
            .ToListAsync(cancellationToken);
    }

    public Task<UserPushDeviceData?> GetActiveAsync(
        Guid deviceId,
        Guid expectedUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserPushDevices
            .AsNoTracking()
            .Where(device =>
                device.Id == deviceId
                && device.UserId == expectedUserId
                && device.IsActive
                && !device.IsDeleted)
            .Select(device => new UserPushDeviceData(
                device.Id,
                device.UserId,
                device.ExpoPushToken,
                device.TokenVersion,
                device.Platform,
                device.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task AcquireRegistrationLockAsync(
        string lockKey,
        CancellationToken cancellationToken)
    {
        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0));",
            cancellationToken);
    }
}

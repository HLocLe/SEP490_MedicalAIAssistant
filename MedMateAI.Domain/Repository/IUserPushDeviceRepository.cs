using MedMateAI.Domain.Common;

namespace MedMateAI.Domain.Repository;

public interface IUserPushDeviceRepository
{
    Task<UserPushDeviceRegistrationResult> RegisterOrUpdateAsync(
        Guid userId,
        string installationId,
        string expoPushToken,
        string platform,
        string? appVersion,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateByInstallationAsync(
        Guid userId,
        string installationId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateIfTokenVersionMatchesAsync(
        Guid deviceId,
        Guid expectedUserId,
        int expectedTokenVersion,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserPushDeviceData>> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<UserPushDeviceData>>>
        GetActiveByUserIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default);

    Task<UserPushDeviceData?> GetActiveAsync(
        Guid deviceId,
        Guid expectedUserId,
        CancellationToken cancellationToken = default);
}

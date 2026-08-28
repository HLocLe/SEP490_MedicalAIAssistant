using MedMateAI.Application.DTOs.PushDevices;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.PushDevices;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class UserPushDeviceService : IUserPushDeviceService
{
    private const int InstallationIdMaxLength = 128;
    private const int ExpoPushTokenMaxLength = 512;
    private const int PlatformMaxLength = 32;
    private const int AppVersionMaxLength = 64;

    private readonly IUnitOfWork _unitOfWork;

    public UserPushDeviceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PushDeviceOperationResult<PushDeviceResponse>> RegisterAsync(
        Guid userId,
        RegisterPushDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || request is null)
        {
            return InvalidRegistration();
        }

        var installationId = request.InstallationId?.Trim();
        var token = request.ExpoPushToken?.Trim();
        var platform = request.Platform?.Trim().ToLowerInvariant();
        var appVersion = string.IsNullOrWhiteSpace(request.AppVersion)
            ? null
            : request.AppVersion.Trim();

        if (string.IsNullOrWhiteSpace(installationId)
            || installationId.Length > InstallationIdMaxLength
            || string.IsNullOrWhiteSpace(token)
            || token.Length > ExpoPushTokenMaxLength
            || !IsExpoPushToken(token)
            || string.IsNullOrWhiteSpace(platform)
            || platform.Length > PlatformMaxLength
            || platform is not ("android" or "ios")
            || appVersion?.Length > AppVersionMaxLength)
        {
            return InvalidRegistration();
        }

        var result = await _unitOfWork.UserPushDevices.RegisterOrUpdateAsync(
            userId,
            installationId,
            token,
            platform,
            appVersion,
            DateTime.UtcNow,
            cancellationToken);

        return result.Status switch
        {
            UserPushDeviceRegistrationStatus.Success when result.Device is not null =>
                PushDeviceOperationResult<PushDeviceResponse>.Ok(
                    Map(result.Device)),
            UserPushDeviceRegistrationStatus.UserNotFound =>
                PushDeviceOperationResult<PushDeviceResponse>.Fail(
                    PushDeviceErrorCode.NotFound,
                    "User was not found."),
            _ => PushDeviceOperationResult<PushDeviceResponse>.Fail(
                PushDeviceErrorCode.Conflict,
                "The push device registration conflicted with another request.")
        };
    }

    public async Task<PushDeviceOperationResult<bool>> DeactivateAsync(
        Guid userId,
        string installationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedInstallationId = installationId?.Trim();
        if (userId == Guid.Empty
            || string.IsNullOrWhiteSpace(normalizedInstallationId)
            || normalizedInstallationId.Length > InstallationIdMaxLength)
        {
            return PushDeviceOperationResult<bool>.Fail(
                PushDeviceErrorCode.InvalidRequest,
                "The push device request is invalid.");
        }

        await _unitOfWork.UserPushDevices.DeactivateByInstallationAsync(
            userId,
            normalizedInstallationId,
            DateTime.UtcNow,
            cancellationToken);

        // Deactivation is deliberately idempotent and does not disclose ownership.
        return PushDeviceOperationResult<bool>.Ok(true);
    }

    private static bool IsExpoPushToken(string token)
    {
        return HasTokenEnvelope(token, "ExpoPushToken[")
               || HasTokenEnvelope(token, "ExponentPushToken[");
    }

    private static bool HasTokenEnvelope(string token, string prefix)
    {
        return token.StartsWith(prefix, StringComparison.Ordinal)
               && token.EndsWith(']')
               && token.Length > prefix.Length + 1;
    }

    private static PushDeviceOperationResult<PushDeviceResponse>
        InvalidRegistration()
    {
        return PushDeviceOperationResult<PushDeviceResponse>.Fail(
            PushDeviceErrorCode.InvalidRequest,
            "The push device registration is invalid.");
    }

    private static PushDeviceResponse Map(UserPushDeviceRegistrationData device)
    {
        return new PushDeviceResponse
        {
            Id = device.Id,
            InstallationId = device.InstallationId,
            Platform = device.Platform,
            IsActive = device.IsActive,
            LastSeenAt = device.LastSeenAt
        };
    }
}

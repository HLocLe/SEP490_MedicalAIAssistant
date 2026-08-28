namespace MedMateAI.Domain.Common;

public sealed record UserPushDeviceData(
    Guid Id,
    Guid UserId,
    string ExpoPushToken,
    int TokenVersion,
    string Platform,
    bool IsActive);

public sealed record UserPushDeviceRegistrationData(
    Guid Id,
    string InstallationId,
    string Platform,
    bool IsActive,
    DateTime LastSeenAt);

public enum UserPushDeviceRegistrationStatus
{
    Success,
    UserNotFound,
    Conflict
}

public sealed record UserPushDeviceRegistrationResult(
    UserPushDeviceRegistrationStatus Status,
    UserPushDeviceRegistrationData? Device = null);

public sealed record PushNotificationProcessingItem(
    Guid Id,
    Guid UserId,
    Guid PushDeviceId,
    string? ExpoPushToken,
    int? TokenVersion,
    bool IsDeviceEligible,
    string NotificationType,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTime? ScheduledAt,
    int AttemptCount);

public sealed record PushReceiptProcessingItem(
    Guid Id,
    Guid UserId,
    Guid PushDeviceId,
    string ProviderMessageId,
    int? ProviderPushTokenVersion,
    string NotificationType,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTime? ScheduledAt,
    int AttemptCount,
    int ReceiptAttemptCount);

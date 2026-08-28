namespace MedMateAI.Application.Models.Notifications;

public sealed record PushNotificationMessage(
    string ExpoPushToken,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data,
    int? TimeToLiveSeconds);

public enum PushSendOutcome
{
    Accepted,
    RetryableFailure,
    InvalidDevice,
    PermanentFailure
}

public sealed record PushSendResult(
    PushSendOutcome Outcome,
    string? ProviderMessageId = null,
    string? ErrorCode = null);

public enum PushReceiptOutcome
{
    Delivered,
    DeviceNotRegistered,
    MessageRateExceeded,
    PermanentFailure
}

public sealed record PushReceiptResult(
    PushReceiptOutcome Outcome,
    string? ErrorCode = null);

public sealed record PushReceiptBatchResult(
    bool Success,
    bool IsRetryableFailure,
    IReadOnlyDictionary<string, PushReceiptResult> Receipts,
    string? ErrorCode = null);

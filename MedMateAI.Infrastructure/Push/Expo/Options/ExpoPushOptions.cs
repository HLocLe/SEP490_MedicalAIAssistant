namespace MedMateAI.Infrastructure.Push.Expo.Options;

public sealed class ExpoPushOptions
{
    public const string SectionName = "ExpoPush";

    public bool Enabled { get; set; } = true;

    public string SendEndpoint { get; set; } =
        "https://exp.host/--/api/v2/push/send";

    public string ReceiptEndpoint { get; set; } =
        "https://exp.host/--/api/v2/push/getReceipts";

    public string? AccessToken { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int ReceiptDelayMinutes { get; set; } = 15;

    public int ReceiptRetryMinutes { get; set; } = 5;

    public int ReceiptMaxAttempts { get; set; } = 6;

    public int ReceiptBatchSize { get; set; } = 100;
}

namespace MedMateAI.Infrastructure.Payments.PayOS;

public sealed class PayOSOptions
{
    public const string SectionName = "PayOS";

    public string ClientId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ChecksumKey { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;

    public string WebhookUrl { get; set; } = string.Empty;

    public int PaymentLinkExpirationMinutes { get; set; } = 15;

    public int PendingReconciliationIntervalMinutes { get; set; } = 5;

    public int PendingReconciliationMinimumAgeMinutes { get; set; } = 1;

    public int PendingCleanupGraceMinutes { get; set; } = 2;

    public int PendingReconciliationBatchSize { get; set; } = 50;
}

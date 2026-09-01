namespace MedMateAI.Application.Options;

public sealed class SaleCampaignNotificationOptions
{
    public const string SectionName = "SaleCampaignNotifications";

    public bool Enabled { get; set; } = true;

    public int PollingSeconds { get; set; } = 60;

    public int UserBatchSize { get; set; } = 100;

    public int CampaignBatchSize { get; set; } = 20;

    public int MaxOffersInEmail { get; set; } = 3;
}

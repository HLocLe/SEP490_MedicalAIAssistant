namespace MedMateAI.Application.DTOs.UserSubscriptions.Responses;

public sealed class SubscriptionUsageResponse
{
    public string QuotaCode { get; set; } = string.Empty;

    public int GrantedCount { get; set; }

    public int UsedCount { get; set; }

    public int ReservedCount { get; set; }

    public int RemainingCount => Math.Max(0, GrantedCount - UsedCount - ReservedCount);
}

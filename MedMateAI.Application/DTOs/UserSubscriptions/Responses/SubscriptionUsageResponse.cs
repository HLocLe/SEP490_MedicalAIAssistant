using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.UserSubscriptions.Responses;

public sealed class SubscriptionUsageResponse
{
    public string QuotaCode { get; set; } = string.Empty;

    public string QuotaName { get; set; } = string.Empty;

    public int LimitValue { get; set; }

    public int UsedCount { get; set; }

    public int ReservedCount { get; set; }

    public int RemainingCount => Math.Max(0, LimitValue - UsedCount - ReservedCount);

    public DateTime CycleStart { get; set; }

    public DateTime CycleEnd { get; set; }

    public QuotaResetPeriod ResetPeriod { get; set; }
}

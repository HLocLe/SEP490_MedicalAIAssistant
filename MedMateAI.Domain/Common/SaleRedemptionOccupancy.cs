namespace MedMateAI.Domain.Common;

public sealed record SaleRedemptionOccupancy(
    Guid SaleCampaignId,
    int ReservedCount,
    int CompletedCount,
    int UserOccupiedCount)
{
    public int OccupiedCount => ReservedCount + CompletedCount;
}

using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Sales.Requests;

public sealed class UpsertSaleCampaignRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? BadgeText { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public SaleCampaignEligibilityType EligibilityType { get; set; }

    public int? MaxRedemptions { get; set; }

    public int? MaxRedemptionsPerUser { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<SaleCampaignPlanRequest> Plans { get; set; } =
        Array.Empty<SaleCampaignPlanRequest>();
}

using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Sales.Responses;

public sealed class SaleCampaignResponse
{
    public Guid Id { get; set; }

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

    public string DisplayStatus { get; set; } = string.Empty;

    public int OccupiedRedemptions { get; set; }

    public int CompletedRedemptions { get; set; }

    public int ReservedRedemptions { get; set; }

    public int? RemainingRedemptions { get; set; }

    public IReadOnlyList<SaleCampaignPlanResponse> Plans { get; set; } =
        Array.Empty<SaleCampaignPlanResponse>();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Responses;

namespace MedMateAI.Application.DTOs.SubscriptionPlans.Responses;

public sealed class SubscriptionPlanResponse
{
    public Guid Id { get; set; }

    public string? PlanName { get; set; }

    public decimal Price { get; set; }

    public int DurationInDays { get; set; }

    public string? FeatureLimitJson { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<SubscriptionPlanQuotaResponse> Quotas { get; set; }
        = Array.Empty<SubscriptionPlanQuotaResponse>();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Responses;

public sealed class SubscriptionPlanQuotaResponse
{
    public Guid Id { get; set; }

    public Guid PlanId { get; set; }

    public Guid QuotaId { get; set; }

    public string QuotaCode { get; set; } = string.Empty;

    public string QuotaName { get; set; } = string.Empty;

    public string? QuotaDescription { get; set; }

    public string Unit { get; set; } = string.Empty;

    public int LimitValue { get; set; }

    public QuotaResetPeriod ResetPeriod { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

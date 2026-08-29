using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Sales.Responses;

public sealed class SaleRedemptionResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string CampaignNameSnapshot { get; set; } = string.Empty;
    public Guid CampaignPlanId { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid UserSubscriptionId { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal FinalPrice { get; set; }
    public int BaseCredit { get; set; }
    public int BonusCredit { get; set; }
    public int GrantedCredit { get; set; }
    public SaleCampaignEligibilityType EligibilityTypeSnapshot { get; set; }
    public SaleRedemptionStatus Status { get; set; }
    public DateTime ReservedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

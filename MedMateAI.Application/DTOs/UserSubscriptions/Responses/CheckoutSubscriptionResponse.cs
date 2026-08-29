namespace MedMateAI.Application.DTOs.UserSubscriptions.Responses;

public sealed class CheckoutSubscriptionResponse
{
    public Guid SubscriptionId { get; set; }

    public Guid PaymentId { get; set; }

    public Guid TransactionId { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string PaymentUrl { get; set; } = string.Empty;

    public string PaymentProvider { get; set; } = "payOS";

    public decimal OriginalPrice { get; set; }

    public decimal FinalPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public int BaseCredit { get; set; }

    public int BonusCredit { get; set; }

    public int GrantedCredit { get; set; }

    public Guid? AppliedSaleCampaignId { get; set; }

    public Guid? AppliedSaleCampaignPlanId { get; set; }

    public string? SaleCampaignName { get; set; }

    public string? SaleBadgeText { get; set; }
}

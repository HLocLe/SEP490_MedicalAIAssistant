using MedMateAI.Application.DTOs.SubscriptionPlans.Responses;

namespace MedMateAI.Application.DTOs.Sales.Responses;

public sealed class SubscriptionPlanOfferResponse
{
    public SubscriptionPlanResponse Plan { get; set; } = null!;
    public decimal OriginalPrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public int BaseCredit { get; set; }
    public int BonusCredit { get; set; }
    public int GrantedCredit { get; set; }
    public SaleOfferResponse? Offer { get; set; }
}

namespace MedMateAI.Application.DTOs.UserSubscriptions.Requests;

public sealed class CheckoutSubscriptionRequest
{
    public Guid PlanId { get; set; }

    public bool AutoRenew { get; set; }

    public Guid? ExpectedOfferId { get; set; }

    public decimal? ExpectedEffectivePrice { get; set; }

    public int? ExpectedGrantedCredit { get; set; }
}

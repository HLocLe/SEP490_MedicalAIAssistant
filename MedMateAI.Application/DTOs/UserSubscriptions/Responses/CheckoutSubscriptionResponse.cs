namespace MedMateAI.Application.DTOs.UserSubscriptions.Responses;

public sealed class CheckoutSubscriptionResponse
{
    public Guid SubscriptionId { get; set; }

    public Guid PaymentId { get; set; }

    public Guid TransactionId { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string PaymentUrl { get; set; } = string.Empty;

    public string PaymentProvider { get; set; } = "payOS";
}

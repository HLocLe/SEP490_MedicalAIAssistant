namespace MedMateAI.Application.DTOs.Payments.PayOS;

public sealed class PayOSCreatePaymentRequest
{
    public long OrderCode { get; set; }

    public int Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;

    public bool UseMobileCallbacks { get; set; }

    public Guid PaymentId { get; set; }

    public Guid SubscriptionId { get; set; }

    public Guid UserId { get; set; }
}

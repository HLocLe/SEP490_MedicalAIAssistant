namespace MedMateAI.Application.DTOs.Payments.Responses;

public sealed class PayOSPaymentStatusResponse
{
    public string OrderCode { get; set; } = string.Empty;

    public Guid? PaymentId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;

    public string SubscriptionStatus { get; set; } = string.Empty;

    public string? ProviderStatus { get; set; }

    public decimal? AmountPaid { get; set; }

    public decimal? AmountRemaining { get; set; }

    public bool IsPaid { get; set; }

    public bool IsActive { get; set; }

    public bool IsCancelled { get; set; }

    public string Message { get; set; } = string.Empty;
}

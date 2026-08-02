namespace MedMateAI.Application.DTOs.Payments.PayOS;

public sealed class PayOSPaymentLinkResult
{
    public long OrderCode { get; set; }

    public string PaymentLinkId { get; set; } = string.Empty;

    public long Amount { get; set; }

    public long AmountPaid { get; set; }

    public long AmountRemaining { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? LatestTransactionReference { get; set; }

    public string? LatestTransactionDescription { get; set; }

    public string? ResponseCode { get; set; }

    public string RawResponse { get; set; } = string.Empty;
}

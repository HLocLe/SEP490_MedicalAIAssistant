using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Payments.Responses;

public sealed class PaymentResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid UserSubscriptionId { get; set; }

    public Guid? PlanId { get; set; }

    public string? PlanName { get; set; }

    public decimal Amount { get; set; }

    public string? Currency { get; set; }

    public PaymentStatus Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? PaymentProvider { get; set; }

    public string? TransactionReference { get; set; }
}

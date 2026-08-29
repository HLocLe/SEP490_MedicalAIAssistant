using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class Payment : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid UserSubscriptionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime? PaidAt { get; set; }

    public UserSubscription UserSubscription { get; set; } = null!;

    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();

    public SaleRedemption? SaleRedemption { get; set; }
}

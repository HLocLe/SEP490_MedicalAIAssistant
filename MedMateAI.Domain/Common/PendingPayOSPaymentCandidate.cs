namespace MedMateAI.Domain.Common;

public sealed record PendingPayOSPaymentCandidate(
    Guid PaymentTransactionId,
    Guid? PaymentId,
    Guid UserSubscriptionId,
    string TransactionReference,
    DateTime CreatedAt);

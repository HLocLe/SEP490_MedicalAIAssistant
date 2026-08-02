using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IPaymentTransactionRepository : IGenericRepository<PaymentTransaction>
{
    Task<PaymentTransaction?> GetByTransactionReferenceAsync(
        string transactionReference,
        CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetByTransactionReferenceForUpdateAsync(
        string transactionReference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentTransaction>> GetByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);
}

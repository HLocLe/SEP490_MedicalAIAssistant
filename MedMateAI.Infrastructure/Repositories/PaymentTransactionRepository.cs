using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class PaymentTransactionRepository
    : GenericRepository<PaymentTransaction>, IPaymentTransactionRepository
{
    private readonly ApplicationDbContext _context;

    public PaymentTransactionRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<PaymentTransaction?> GetByTransactionReferenceAsync(
        string transactionReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionReference))
        {
            return null;
        }

        var normalized = transactionReference.Trim();

        return await _context.PaymentTransactions
            .AsNoTracking()
            .Include(x => x.Payment)
            .ThenInclude(x => x!.UserSubscription)
            .ThenInclude(x => x.Plan)
            .Include(x => x.UserSubscription)
            .ThenInclude(x => x.Plan)
            .FirstOrDefaultAsync(
                x =>
                    !x.IsDeleted
                    && x.TransactionReference != null
                    && x.TransactionReference == normalized,
                cancellationToken);
    }

    public async Task<PaymentTransaction?> GetByTransactionReferenceForUpdateAsync(
        string transactionReference,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        if (string.IsNullOrWhiteSpace(transactionReference))
        {
            return null;
        }

        var normalized = transactionReference.Trim();

        var transactions = await _context.PaymentTransactions
            .FromSqlInterpolated($"""
                SELECT *
                FROM "PaymentTransaction"
                WHERE "TransactionReference" = {normalized}
                  AND "IsDeleted" = FALSE
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var transaction = transactions.SingleOrDefault();
        if (transaction is null)
        {
            return null;
        }

        await _context.Entry(transaction)
            .Reference(currentTransaction => currentTransaction.Payment)
            .Query()
            .Include(payment => payment.UserSubscription)
            .ThenInclude(subscription => subscription.Plan)
            .LoadAsync(cancellationToken);

        await _context.Entry(transaction)
            .Reference(currentTransaction => currentTransaction.UserSubscription)
            .Query()
            .Include(subscription => subscription.Plan)
            .LoadAsync(cancellationToken);

        return transaction;
    }

    public async Task<IReadOnlyList<PaymentTransaction>> GetByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        if (paymentId == Guid.Empty)
        {
            return Array.Empty<PaymentTransaction>();
        }

        return await _context.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.PaymentId == paymentId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Payment transaction locking requires an active database transaction.");
        }
    }
}

using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
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

    public async Task<PaymentTransaction?> GetLatestPayOSByUserSubscriptionIdAsync(
        Guid userSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (userSubscriptionId == Guid.Empty)
        {
            return null;
        }

        return await _context.PaymentTransactions
            .AsNoTracking()
            .Include(transaction => transaction.Payment)
            .ThenInclude(payment => payment!.UserSubscription)
            .ThenInclude(subscription => subscription.Plan)
            .Include(transaction => transaction.UserSubscription)
            .ThenInclude(subscription => subscription.Plan)
            .Where(transaction =>
                !transaction.IsDeleted
                && transaction.UserSubscriptionId == userSubscriptionId
                && transaction.PaymentProvider != null
                && transaction.PaymentProvider.ToLower() == "payos"
                && transaction.TransactionReference != null)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingPayOSPaymentCandidate>> GetPendingPayOSCandidatesAsync(
        DateTime createdBeforeOrAt,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedBatchSize = Math.Clamp(batchSize, 1, 500);

        return await _context.PaymentTransactions
            .AsNoTracking()
            .Where(transaction =>
                !transaction.IsDeleted
                && transaction.PaymentProvider != null
                && transaction.PaymentProvider.ToLower() == "payos"
                && transaction.TransactionReference != null
                && transaction.Payment != null
                && !transaction.Payment.IsDeleted
                && transaction.Payment.Status == PaymentStatus.Pending
                && !transaction.UserSubscription.IsDeleted
                && transaction.UserSubscription.Status == SubscriptionStatus.Pending
                && transaction.CreatedAt <= createdBeforeOrAt)
            .OrderBy(transaction => transaction.CreatedAt)
            .ThenBy(transaction => transaction.Id)
            .Take(normalizedBatchSize)
            .Select(transaction => new PendingPayOSPaymentCandidate(
                transaction.Id,
                transaction.PaymentId,
                transaction.UserSubscriptionId,
                transaction.TransactionReference!,
                transaction.CreatedAt))
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

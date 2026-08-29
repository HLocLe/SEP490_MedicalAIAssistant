using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class SaleRedemptionRepository : ISaleRedemptionRepository
{
    private readonly ApplicationDbContext _context;

    public SaleRedemptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> LockUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();
        var users = await _context.Users
            .FromSqlInterpolated($"""
                SELECT *
                FROM "AspNetUsers"
                WHERE "Id" = {userId}
                  AND "IsDeleted" = FALSE
                FOR UPDATE
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return users.Count == 1;
    }

    public Task<bool> HasSuccessfulPurchaseAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Payments
            .AsNoTracking()
            .AnyAsync(
                payment =>
                    payment.UserId == userId
                    && !payment.IsDeleted
                    && payment.PaidAt.HasValue,
                cancellationToken);
    }

    public Task<bool> HasFirstPurchaseReservationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.SaleRedemptions
            .AsNoTracking()
            .AnyAsync(
                redemption =>
                    redemption.UserId == userId
                    && !redemption.IsDeleted
                    && redemption.EligibilityTypeSnapshot
                        == SaleCampaignEligibilityType.FirstPurchase
                    && (redemption.Status == SaleRedemptionStatus.Reserved
                        || redemption.Status == SaleRedemptionStatus.Completed),
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, SaleRedemptionOccupancy>> GetOccupancyAsync(
        IReadOnlyCollection<Guid> campaignIds,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (campaignIds.Count == 0)
        {
            return new Dictionary<Guid, SaleRedemptionOccupancy>();
        }

        var normalizedIds = campaignIds.Distinct().ToArray();
        var rows = await _context.SaleRedemptions
            .AsNoTracking()
            .Where(redemption =>
                normalizedIds.Contains(redemption.SaleCampaignId)
                && !redemption.IsDeleted
                && (redemption.Status == SaleRedemptionStatus.Reserved
                    || redemption.Status == SaleRedemptionStatus.Completed))
            .GroupBy(redemption => redemption.SaleCampaignId)
            .Select(group => new
            {
                CampaignId = group.Key,
                ReservedCount = group.Count(redemption =>
                    redemption.Status == SaleRedemptionStatus.Reserved),
                CompletedCount = group.Count(redemption =>
                    redemption.Status == SaleRedemptionStatus.Completed),
                UserOccupiedCount = userId.HasValue
                    ? group.Count(redemption => redemption.UserId == userId.Value)
                    : 0
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.CampaignId,
            row => new SaleRedemptionOccupancy(
                row.CampaignId,
                row.ReservedCount,
                row.CompletedCount,
                row.UserOccupiedCount));
    }

    public async Task<int> GetHighestUserOccupiedCountAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var counts = await _context.SaleRedemptions
            .AsNoTracking()
            .Where(redemption =>
                redemption.SaleCampaignId == campaignId
                && !redemption.IsDeleted
                && (redemption.Status == SaleRedemptionStatus.Reserved
                    || redemption.Status == SaleRedemptionStatus.Completed))
            .GroupBy(redemption => redemption.UserId)
            .Select(group => group.Count())
            .ToListAsync(cancellationToken);
        return counts.Count == 0 ? 0 : counts.Max();
    }

    public Task<bool> HasHistoryAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        return _context.SaleRedemptions
            .AsNoTracking()
            .AnyAsync(
                redemption => redemption.SaleCampaignId == campaignId,
                cancellationToken);
    }

    public async Task<SaleRedemption?> GetByPaymentIdForUpdateAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();
        var redemptions = await _context.SaleRedemptions
            .FromSqlInterpolated($"""
                SELECT *
                FROM "SaleRedemption"
                WHERE "PaymentId" = {paymentId}
                  AND "IsDeleted" = FALSE
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);
        return redemptions.SingleOrDefault();
    }

    public async Task<PagedResult<SaleRedemption>> GetPagedByCampaignAsync(
        Guid campaignId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 100);
        var query = _context.SaleRedemptions
            .AsNoTracking()
            .Where(redemption =>
                redemption.SaleCampaignId == campaignId
                && !redemption.IsDeleted);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(redemption => redemption.CreatedAt)
            .ThenByDescending(redemption => redemption.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SaleRedemption>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)normalizedPageSize),
            Items = items
        };
    }

    public void Add(SaleRedemption redemption)
    {
        _context.SaleRedemptions.Add(redemption);
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Sale redemption locking requires an active database transaction.");
        }
    }
}

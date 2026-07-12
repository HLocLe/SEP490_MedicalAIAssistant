using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class PaymentRepository
    : GenericRepository<Payment>, IPaymentRepository
{
    private readonly ApplicationDbContext _context;

    public PaymentRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<PagedResult<Payment>> GetPagedWithSubscriptionAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : pageSize;
        normalizedPageSize = normalizedPageSize > 100 ? 100 : normalizedPageSize;

        var query = BuildDetailsQuery()
            .Where(x => !x.IsDeleted);

        return await ToPagedResultAsync(
            query,
            normalizedPageNumber,
            normalizedPageSize,
            cancellationToken);
    }

    public async Task<PagedResult<Payment>> GetPagedByUserIdWithSubscriptionAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : pageSize;
        normalizedPageSize = normalizedPageSize > 100 ? 100 : normalizedPageSize;

        if (userId == Guid.Empty)
        {
            return CreateEmptyPagedResult(normalizedPageNumber, normalizedPageSize);
        }

        var query = BuildDetailsQuery()
            .Where(x => x.UserId == userId && !x.IsDeleted);

        return await ToPagedResultAsync(
            query,
            normalizedPageNumber,
            normalizedPageSize,
            cancellationToken);
    }

    public async Task<Payment?> GetByIdWithSubscriptionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _context.Payments
            .Include(x => x.UserSubscription)
            .ThenInclude(x => x.Plan)
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Payment?> GetByIdAndUserIdWithSubscriptionAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        return await BuildDetailsQuery()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId && !x.IsDeleted,
                cancellationToken);
    }

    private IQueryable<Payment> BuildDetailsQuery()
    {
        return _context.Payments
            .AsNoTracking()
            .Include(x => x.UserSubscription)
            .ThenInclude(x => x.Plan)
            .Include(x => x.Transactions);
    }

    private static async Task<PagedResult<Payment>> ToPagedResultAsync(
        IQueryable<Payment> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<Payment>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items,
        };
    }

    private static PagedResult<Payment> CreateEmptyPagedResult(
        int pageNumber,
        int pageSize)
    {
        return new PagedResult<Payment>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = 0,
            TotalPages = 0,
            Items = Array.Empty<Payment>(),
        };
    }
}

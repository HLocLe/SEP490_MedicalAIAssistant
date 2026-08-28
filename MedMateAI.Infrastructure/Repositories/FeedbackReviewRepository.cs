using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class FeedbackReviewRepository
    : GenericRepository<FeedbackReview>, IFeedbackReviewRepository
{
    private const string ApprovedStatus = "approved";

    private readonly ApplicationDbContext _context;

    public FeedbackReviewRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<PagedResult<FeedbackReview>> GetPagedWithDetailsAsync(
        int pageNumber,
        int pageSize,
        Guid? facilityId = null,
        Guid? userId = null,
        string? status = null,
        int? rating = null,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPageNumber, normalizedPageSize) = NormalizePaging(pageNumber, pageSize);

        var query = BuildDetailsQuery();

        if (facilityId.HasValue && facilityId.Value != Guid.Empty)
        {
            query = query.Where(x => x.FacilityId == facilityId.Value);
        }

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLower();
            query = query.Where(x => x.Status != null && x.Status.ToLower() == normalizedStatus);
        }

        if (rating.HasValue)
        {
            query = query.Where(x => x.Rating == rating.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new PagedResult<FeedbackReview>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items,
        };
    }

    public async Task<PagedResult<FeedbackReview>> GetApprovedByFacilityAsync(
        Guid facilityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPageNumber, normalizedPageSize) = NormalizePaging(pageNumber, pageSize);

        var query = BuildDetailsQuery()
            .Where(x =>
                x.FacilityId == facilityId
                && x.Status != null
                && x.Status.ToLower() == ApprovedStatus);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new PagedResult<FeedbackReview>
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items,
        };
    }

    public async Task<FeedbackReview?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await BuildDetailsQuery()
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<FeedbackReview?> GetByUserAndFacilityAsync(
        Guid userId,
        Guid facilityId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || facilityId == Guid.Empty)
        {
            return null;
        }

        return await BuildDetailsQuery()
            .Where(x => x.UserId == userId && x.FacilityId == facilityId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, FacilityRatingSummary>> GetApprovedRatingSummariesByFacilityIdsAsync(
        IReadOnlyCollection<Guid> facilityIds,
        CancellationToken cancellationToken = default)
    {
        if (facilityIds.Count == 0)
        {
            return new Dictionary<Guid, FacilityRatingSummary>();
        }

        var summaries = await _context.FeedbackReviews
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted
                && x.Status != null
                && x.Status.ToLower() == ApprovedStatus
                && facilityIds.Contains(x.FacilityId))
            .GroupBy(x => x.FacilityId)
            .Select(group => new
            {
                FacilityId = group.Key,
                AverageRating = group.Average(review => review.Rating),
                ReviewCount = group.Count(),
            })
            .ToListAsync(cancellationToken);

        return summaries.ToDictionary(
            summary => summary.FacilityId,
            summary => new FacilityRatingSummary(summary.AverageRating, summary.ReviewCount));
    }

    private IQueryable<FeedbackReview> BuildDetailsQuery()
    {
        return _context.FeedbackReviews
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Include(x => x.Facility);
    }

    private static (int PageNumber, int PageSize) NormalizePaging(int pageNumber, int pageSize)
    {
        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 10 : pageSize;
        normalizedPageSize = normalizedPageSize > 100 ? 100 : normalizedPageSize;
        return (normalizedPageNumber, normalizedPageSize);
    }
}

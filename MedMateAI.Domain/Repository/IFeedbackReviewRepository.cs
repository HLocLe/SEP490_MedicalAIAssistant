using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IFeedbackReviewRepository : IGenericRepository<FeedbackReview>
{
    Task<PagedResult<FeedbackReview>> GetPagedWithDetailsAsync(
        int pageNumber,
        int pageSize,
        Guid? facilityId = null,
        Guid? userId = null,
        string? status = null,
        int? rating = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<FeedbackReview>> GetApprovedByFacilityAsync(
        Guid facilityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<FeedbackReview?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<FeedbackReview?> GetByUserAndFacilityAsync(
        Guid userId,
        Guid facilityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, FacilityRatingSummary>> GetApprovedRatingSummariesByFacilityIdsAsync(
        IReadOnlyCollection<Guid> facilityIds,
        CancellationToken cancellationToken = default);
}

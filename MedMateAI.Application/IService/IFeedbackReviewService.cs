using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.FeedbackReviews.Requests;
using MedMateAI.Application.DTOs.FeedbackReviews.Responses;

namespace MedMateAI.Application.IService;

public interface IFeedbackReviewService
{
    Task<PagedResponse<FeedbackReviewResponse>> ListFeedbackReviewsAsync(
        int pageNumber,
        int pageSize,
        Guid? facilityId = null,
        Guid? userId = null,
        string? status = null,
        int? rating = null,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<FeedbackReviewResponse>> ListApprovedFacilityReviewsAsync(
        Guid facilityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<FeedbackReviewResponse>> ListFeedbackReviewsByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<FeedbackReviewResponse?> GetFeedbackReviewByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, FeedbackReviewResponse? Data)> CreateFeedbackReviewAsync(
        CreateFeedbackReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, FeedbackReviewResponse? Data)> UpdateFeedbackReviewAsync(
        Guid id,
        UpdateFeedbackReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, FeedbackReviewResponse? Data)> UpdateFeedbackReviewStatusAsync(
        Guid id,
        UpdateFeedbackReviewStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteFeedbackReviewAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

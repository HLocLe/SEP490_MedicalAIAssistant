using System.Security.Claims;
using System.Text.Json;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.FeedbackReviews.Requests;
using MedMateAI.Application.DTOs.FeedbackReviews.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;

namespace MedMateAI.Application.Service;

public sealed class FeedbackReviewService : IFeedbackReviewService
{
    private const string FeedbackReviewCacheKeyPrefix = "feedback-reviews:";
    private const string ApprovedStatus = "Approved";
    private const string HiddenStatus = "Hidden";
    private const string RejectedStatus = "Rejected";
    private const string PendingStatus = "Pending";
    private const int MaxCommentLength = 1000;
    private const int ImageUrlMaxLength = 2048;
    private const int MaxImageKeyLength = 100;
    private const int MaxFeedbackReviewImages = 5;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheTtl,
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FeedbackReviewService(
        IUnitOfWork unitOfWork,
        IDistributedCache cache,
        IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResponse<FeedbackReviewResponse>> ListFeedbackReviewsAsync(
        int pageNumber,
        int pageSize,
        Guid? facilityId = null,
        Guid? userId = null,
        string? status = null,
        int? rating = null,
        CancellationToken cancellationToken = default)
    {
        var paged = await _unitOfWork.FeedbackReviews.GetPagedWithDetailsAsync(
            pageNumber,
            pageSize,
            facilityId,
            userId,
            NormalizeText(status),
            rating,
            cancellationToken);

        return MapToPagedResponse(paged);
    }

    public async Task<PagedResponse<FeedbackReviewResponse>> ListApprovedFacilityReviewsAsync(
        Guid facilityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paged = await _unitOfWork.FeedbackReviews.GetApprovedByFacilityAsync(
            facilityId,
            pageNumber,
            pageSize,
            cancellationToken);

        return MapToPagedResponse(paged);
    }

    public async Task<FeedbackReviewResponse?> GetFeedbackReviewByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var cacheKey = GetFeedbackReviewCacheKey(id);
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedResponse = JsonSerializer.Deserialize<FeedbackReviewResponse>(cached);
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }

        var entity = await _unitOfWork.FeedbackReviews.GetByIdWithDetailsAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var response = MapToResponse(entity);
        await CacheFeedbackReviewResponseAsync(response, cancellationToken);

        return response;
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, FeedbackReviewResponse? Data)> CreateFeedbackReviewAsync(
        CreateFeedbackReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body is required." }, null);
        }

        var errors = new List<string>();

        if (request.FacilityId == Guid.Empty)
        {
            errors.Add("FacilityId is required.");
        }

        if (!IsRatingValid(request.Rating))
        {
            errors.Add("Rating must be between 1 and 5.");
        }

        var comment = NormalizeText(request.Comment);
        if (comment is not null && comment.Length > MaxCommentLength)
        {
            errors.Add($"Comment must be less than or equal to {MaxCommentLength} characters.");
        }

        var imageUrls = NormalizeCreateImageUrls(request.ImageUrls, errors);

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            errors.Add("User is not authenticated.");
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        var facility = await _unitOfWork.MedicalFacilities.FirstOrDefaultAsync(
            x => x.Id == request.FacilityId && !x.IsDeleted,
            asNoTracking: true,
            cancellationToken: cancellationToken);

        if (facility is null)
        {
            return (false, new[] { "Medical facility not found." }, null);
        }

        if (!facility.IsActive)
        {
            return (false, new[] { "Medical facility is not active." }, null);
        }

        var existingReview = await _unitOfWork.FeedbackReviews.GetByUserAndFacilityAsync(
            userId!.Value,
            request.FacilityId,
            cancellationToken);

        if (existingReview is not null)
        {
            return (false, new[] { "You have already reviewed this facility." }, null);
        }

        var entity = new FeedbackReview
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            FacilityId = request.FacilityId,
            Rating = request.Rating,
            Comment = comment,
            ImageUrls = imageUrls,
            Status = ApprovedStatus,
            CreatedAt = DateTime.UtcNow,
        };

        _unitOfWork.FeedbackReviews.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.FeedbackReviews.GetByIdWithDetailsAsync(entity.Id, cancellationToken);
        var response = MapToResponse(created ?? entity);
        await CacheFeedbackReviewResponseAsync(response, cancellationToken);

        return (true, Array.Empty<string>(), response);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, FeedbackReviewResponse? Data)> UpdateFeedbackReviewAsync(
        Guid id,
        UpdateFeedbackReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid feedback review id." }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body is required." }, null);
        }

        var entity = await _unitOfWork.FeedbackReviews.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Feedback review not found." }, null);
        }

        var validation = ValidateUpdateFeedbackReviewRequest(request, entity.ImageUrls);
        if (validation.Errors.Count > 0)
        {
            return (false, false, validation.Errors, null);
        }

        ApplyFeedbackReviewUpdates(
            entity,
            request,
            validation.CommentFromRequest,
            validation.ImageUrlsFromRequest);

        _unitOfWork.FeedbackReviews.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(GetFeedbackReviewCacheKey(id), cancellationToken);

        var response = await ReloadMapAndCacheFeedbackReviewAsync(id, cancellationToken);
        if (response is null)
        {
            return (false, true, new[] { "Feedback review not found." }, null);
        }

        return (true, false, Array.Empty<string>(), response);
    }

    private static FeedbackReviewUpdateValidationResult ValidateUpdateFeedbackReviewRequest(
        UpdateFeedbackReviewRequest request,
        Dictionary<string, string>? currentImageUrls)
    {
        var errors = new List<string>();
        string? commentFromRequest = null;
        Dictionary<string, string>? imageUrlsFromRequest = null;

        if (request.Rating.HasValue && !IsRatingValid(request.Rating.Value))
        {
            errors.Add("Rating must be between 1 and 5.");
        }

        if (request.Comment is not null)
        {
            commentFromRequest = NormalizeText(request.Comment);
            if (commentFromRequest is not null && commentFromRequest.Length > MaxCommentLength)
            {
                errors.Add($"Comment must be less than or equal to {MaxCommentLength} characters.");
            }
        }

        if (request.ImageUrls is not null)
        {
            imageUrlsFromRequest = PatchImageUrls(currentImageUrls, request.ImageUrls, errors);
        }

        return new FeedbackReviewUpdateValidationResult(
            errors,
            commentFromRequest,
            imageUrlsFromRequest);
    }

    private static void ApplyFeedbackReviewUpdates(
        FeedbackReview entity,
        UpdateFeedbackReviewRequest request,
        string? commentFromRequest,
        Dictionary<string, string>? imageUrlsFromRequest)
    {
        if (request.Rating.HasValue)
        {
            entity.Rating = request.Rating.Value;
        }

        if (request.Comment is not null)
        {
            entity.Comment = commentFromRequest;
        }

        if (request.ImageUrls is not null)
        {
            entity.ImageUrls = imageUrlsFromRequest ?? new Dictionary<string, string>();
        }

        entity.UpdatedAt = DateTime.UtcNow;
    }

    private sealed record FeedbackReviewUpdateValidationResult(
        List<string> Errors,
        string? CommentFromRequest,
        Dictionary<string, string>? ImageUrlsFromRequest);

    private sealed record ImageUrlPatchEntry(
        string Key,
        string? Url);

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, FeedbackReviewResponse? Data)> UpdateFeedbackReviewStatusAsync(
        Guid id,
        UpdateFeedbackReviewStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid feedback review id." }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body is required." }, null);
        }

        var entity = await _unitOfWork.FeedbackReviews.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Feedback review not found." }, null);
        }

        if (!TryNormalizeStatus(request.Status, out var normalizedStatus))
        {
            return (false, false, new[] { "Status is invalid." }, null);
        }

        entity.Status = normalizedStatus;
        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.FeedbackReviews.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(GetFeedbackReviewCacheKey(id), cancellationToken);

        var response = await ReloadMapAndCacheFeedbackReviewAsync(id, cancellationToken);
        if (response is null)
        {
            return (false, true, new[] { "Feedback review not found." }, null);
        }

        return (true, false, Array.Empty<string>(), response);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteFeedbackReviewAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid feedback review id." });
        }

        var entity = await _unitOfWork.FeedbackReviews.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Feedback review not found." });
        }

        var utcNow = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = utcNow;
        entity.UpdatedAt = utcNow;

        _unitOfWork.FeedbackReviews.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(GetFeedbackReviewCacheKey(id), cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private static PagedResponse<FeedbackReviewResponse> MapToPagedResponse(
        PagedResult<FeedbackReview> paged)
    {
        return new PagedResponse<FeedbackReviewResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(MapToResponse).ToList(),
        };
    }

    private static FeedbackReviewResponse MapToResponse(FeedbackReview entity)
    {
        return new FeedbackReviewResponse
        {
            Id = entity.Id,
            UserId = entity.UserId,
            FacilityId = entity.FacilityId,
            FacilityName = entity.Facility?.FacilityName,
            FacilityAddress = entity.Facility?.Address,
            Rating = entity.Rating,
            Comment = entity.Comment,
            ImageUrls = entity.ImageUrls ?? new Dictionary<string, string>(),
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private async Task<FeedbackReviewResponse?> ReloadMapAndCacheFeedbackReviewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _unitOfWork.FeedbackReviews.GetByIdWithDetailsAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var response = MapToResponse(entity);
        await CacheFeedbackReviewResponseAsync(response, cancellationToken);

        return response;
    }

    private async Task CacheFeedbackReviewResponseAsync(
        FeedbackReviewResponse response,
        CancellationToken cancellationToken)
    {
        await _cache.SetStringAsync(
            GetFeedbackReviewCacheKey(response.Id),
            JsonSerializer.Serialize(response),
            CacheOptions,
            cancellationToken);
    }

    private Guid? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdValue =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("userId");

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private static bool IsRatingValid(int rating)
    {
        return rating is >= 1 and <= 5;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static Dictionary<string, string> NormalizeCreateImageUrls(
        Dictionary<string, string>? imageUrls,
        ICollection<string> errors)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (imageUrls is null || imageUrls.Count == 0)
        {
            return normalized;
        }

        var seenKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in imageUrls)
        {
            var key = NormalizeText(pair.Key);
            var url = NormalizeText(pair.Value);

            if (key is null)
            {
                errors.Add("ImageUrls contains an invalid empty key.");
                continue;
            }

            if (key.Length > MaxImageKeyLength)
            {
                errors.Add($"ImageUrls key must be {MaxImageKeyLength} characters or fewer.");
                continue;
            }

            if (seenKeys.ContainsKey(key))
            {
                errors.Add("ImageUrls contains duplicate keys.");
                continue;
            }

            seenKeys.Add(key, key);

            if (url is null)
            {
                errors.Add("ImageUrls contains an invalid empty URL.");
                continue;
            }

            ValidateImageUrl(url, errors);
            normalized.Add(key, url);
        }

        if (normalized.Count > MaxFeedbackReviewImages)
        {
            errors.Add("ImageUrls cannot contain more than 5 images.");
        }

        return normalized;
    }

    private static Dictionary<string, string> PatchImageUrls(
        Dictionary<string, string>? currentImageUrls,
        Dictionary<string, string?>? patchImageUrls,
        ICollection<string> errors)
    {
        var result = new Dictionary<string, string>(
            currentImageUrls ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);

        if (patchImageUrls is null || patchImageUrls.Count == 0)
        {
            return result;
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in patchImageUrls)
        {
            var patch = NormalizePatchImageUrlEntry(pair, seenKeys, errors);

            if (patch is null)
            {
                continue;
            }

            ApplyImageUrlPatch(result, patch, errors);
        }

        ValidateImageUrlsCount(result, errors);

        return result;
    }

    private static ImageUrlPatchEntry? NormalizePatchImageUrlEntry(
        KeyValuePair<string, string?> pair,
        ISet<string> seenKeys,
        ICollection<string> errors)
    {
        var key = NormalizeText(pair.Key);
        var url = NormalizeText(pair.Value);

        if (key is null)
        {
            errors.Add("ImageUrls contains an invalid empty key.");
            return null;
        }

        if (key.Length > MaxImageKeyLength)
        {
            errors.Add($"ImageUrls key must be {MaxImageKeyLength} characters or fewer.");
            return null;
        }

        if (!seenKeys.Add(key))
        {
            errors.Add("ImageUrls contains duplicate keys.");
            return null;
        }

        return new ImageUrlPatchEntry(key, url);
    }

    private static void ApplyImageUrlPatch(
        Dictionary<string, string> imageUrls,
        ImageUrlPatchEntry patch,
        ICollection<string> errors)
    {
        if (patch.Url is null)
        {
            if (imageUrls.ContainsKey(patch.Key))
            {
                imageUrls.Remove(patch.Key);
            }

            return;
        }

        ValidateImageUrl(patch.Url, errors);

        if (imageUrls.ContainsKey(patch.Key))
        {
            imageUrls[patch.Key] = patch.Url;
        }
        else
        {
            imageUrls.Add(patch.Key, patch.Url);
        }
    }

    private static void ValidateImageUrlsCount(
        IReadOnlyDictionary<string, string> imageUrls,
        ICollection<string> errors)
    {
        if (imageUrls.Count > MaxFeedbackReviewImages)
        {
            errors.Add("ImageUrls cannot contain more than 5 images.");
        }
    }

    private static bool IsValidHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static void ValidateImageUrl(string imageUrl, ICollection<string> errors)
    {
        if (imageUrl.Length > ImageUrlMaxLength)
        {
            errors.Add("ImageUrl must be 2048 characters or fewer.");
        }

        if (!IsValidHttpUrl(imageUrl))
        {
            errors.Add("ImageUrl must be a valid absolute http or https URL.");
        }
    }

    private static bool TryNormalizeStatus(string? rawStatus, out string normalizedStatus)
    {
        normalizedStatus = string.Empty;
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return false;
        }

        var value = rawStatus.Trim().ToLowerInvariant();
        switch (value)
        {
            case "approved":
                normalizedStatus = ApprovedStatus;
                return true;
            case "hidden":
                normalizedStatus = HiddenStatus;
                return true;
            case "rejected":
                normalizedStatus = RejectedStatus;
                return true;
            case "pending":
                normalizedStatus = PendingStatus;
                return true;
            default:
                return false;
        }
    }

    private static string GetFeedbackReviewCacheKey(Guid id)
    {
        return $"{FeedbackReviewCacheKeyPrefix}{id}";
    }
}

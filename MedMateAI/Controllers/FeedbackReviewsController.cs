using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.FeedbackReviews.Requests;
using MedMateAI.Application.DTOs.FeedbackReviews.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Authorize]
[Route("api/feedback-reviews")]
public sealed class FeedbackReviewsController : ControllerBase
{
    private const string InvalidIdMessage = "Id feedback không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy feedback";
    private const string UnauthenticatedError = "Người dùng chưa đăng nhập";

    private readonly IFeedbackReviewService _feedbackReviewService;

    public FeedbackReviewsController(IFeedbackReviewService feedbackReviewService)
    {
        _feedbackReviewService = feedbackReviewService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<FeedbackReviewResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<FeedbackReviewResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] Guid? facilityId,
        [FromQuery] Guid? userId,
        [FromQuery] string? status,
        [FromQuery] int? rating,
        CancellationToken cancellationToken = default)
    {
        if (facilityId.HasValue && facilityId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<FeedbackReviewResponse>>("Id cơ sở y tế không hợp lệ"));
        }

        if (userId.HasValue && userId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<FeedbackReviewResponse>>("Id người dùng không hợp lệ"));
        }

        if (rating.HasValue && (rating.Value < 1 || rating.Value > 5))
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<PagedResponse<FeedbackReviewResponse>>(
                new[] { "Bộ lọc rating phải từ 1 đến 5" },
                "Bộ lọc rating không hợp lệ"));
        }

        var data = await _feedbackReviewService.ListFeedbackReviewsAsync(
            query.PageNumber,
            query.PageSize,
            facilityId,
            userId,
            status,
            rating,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("facility/{facilityId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<FeedbackReviewResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<FeedbackReviewResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListByFacility(
        Guid facilityId,
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (facilityId == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<FeedbackReviewResponse>>("Id cơ sở y tế không hợp lệ"));
        }

        var data = await _feedbackReviewService.ListApprovedFacilityReviewsAsync(
            facilityId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<FeedbackReviewResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<FeedbackReviewResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<FeedbackReviewResponse>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListByUserId(
        Guid userId,
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<FeedbackReviewResponse>>("Id người dùng không hợp lệ"));
        }

        var (forbidden, data) = await _feedbackReviewService.ListFeedbackReviewsByUserIdAsync(
            userId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (forbidden || data is null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponseFactory.Fail<PagedResponse<FeedbackReviewResponse>>(
                    "Không có quyền xem feedback của người dùng này"));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<FeedbackReviewResponse>(InvalidIdMessage));
        }

        var data = await _feedbackReviewService.GetFeedbackReviewByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<FeedbackReviewResponse>(NotFoundMessage));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFeedbackReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ok, errors, data) = await _feedbackReviewService.CreateFeedbackReviewAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            var errorList = errors.ToList();

            if (errorList.Contains(UnauthenticatedError, StringComparer.Ordinal))
            {
                return Unauthorized(ApiResponseFactory.FailFromErrors<FeedbackReviewResponse>(
                    errorList,
                    "Chưa đăng nhập"));
            }

            return BadRequest(ApiResponseFactory.FailFromErrors<FeedbackReviewResponse>(
                errorList,
                "Tạo feedback thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo feedback thành công"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateFeedbackReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<FeedbackReviewResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _feedbackReviewService.UpdateFeedbackReviewAsync(
            id,
            request,
            cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<FeedbackReviewResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<FeedbackReviewResponse>(
                errors,
                "Cập nhật feedback thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật feedback thành công"));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackReviewResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateFeedbackReviewStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<FeedbackReviewResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _feedbackReviewService.UpdateFeedbackReviewStatusAsync(
            id,
            request,
            cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<FeedbackReviewResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<FeedbackReviewResponse>(
                errors,
                "Cập nhật trạng thái feedback thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật trạng thái feedback thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _feedbackReviewService.SoftDeleteFeedbackReviewAsync(
            id,
            cancellationToken);

        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            NotFoundMessage,
            "Xóa feedback thất bại",
            "Xóa feedback thành công");
    }
}

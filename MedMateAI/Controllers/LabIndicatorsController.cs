using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabIndicators.Requests;
using MedMateAI.Application.DTOs.LabIndicators.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/lab-indicators")]
public sealed class LabIndicatorsController : ControllerBase
{
    private const string InvalidIdMessage = "Id chỉ số xét nghiệm không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy chỉ số xét nghiệm";

    private readonly ILabIndicatorService _labIndicatorService;

    public LabIndicatorsController(ILabIndicatorService labIndicatorService)
    {
        _labIndicatorService = labIndicatorService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<LabIndicatorResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var data = await _labIndicatorService.ListLabIndicatorsAsync(
            query.PageNumber,
            query.PageSize,
            search,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorDetailResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<LabIndicatorDetailResponse>(InvalidIdMessage));
        }

        var data = await _labIndicatorService.GetLabIndicatorByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<LabIndicatorDetailResponse>(NotFoundMessage));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLabIndicatorRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _labIndicatorService.CreateLabIndicatorAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<LabIndicatorResponse>(errors, "Tạo chỉ số xét nghiệm thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo chỉ số xét nghiệm thành công"));
    }

    [HttpPost("with-details")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorDetailResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWithDetails(
        [FromBody] CreateLabIndicatorWithDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _labIndicatorService.CreateLabIndicatorWithDetailsAsync(
            request,
            cancellationToken);

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<LabIndicatorDetailResponse>(
                errors,
                "Tạo chỉ số xét nghiệm kèm chi tiết thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo chỉ số xét nghiệm kèm chi tiết thành công"));
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkCreate(
        [FromBody] BulkCreateLabIndicatorsRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _labIndicatorService.BulkCreateLabIndicatorsAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<IReadOnlyList<LabIndicatorResponse>>(
                errors,
                "Bulk create lab indicators failed."));
        }

        return Ok(ApiResponseFactory.Success(data, $"{data.Count} lab indicator(s) created."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLabIndicatorRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<LabIndicatorResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.UpdateLabIndicatorAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<LabIndicatorResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<LabIndicatorResponse>(errors, "Cập nhật chỉ số xét nghiệm thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật chỉ số xét nghiệm thành công"));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _labIndicatorService.SoftDeleteLabIndicatorAsync(id, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            NotFoundMessage,
            "Xóa chỉ số xét nghiệm thất bại",
            "Xóa chỉ số xét nghiệm thành công");
    }

    [HttpGet("{id:guid}/aliases")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAliases(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorAliasResponse>>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.GetAliasesByIndicatorIdAsync(id, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "OK", "Lấy danh sách alias thất bại", NotFoundMessage);
    }

    [HttpGet("{id:guid}/reference-ranges")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReferenceRanges(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorReferenceRangeResponse>>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.GetReferenceRangesByIndicatorIdAsync(id, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "OK", "Lấy khoảng tham chiếu thất bại", NotFoundMessage);
    }

    [HttpGet("{id:guid}/advice")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdviceCaches(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorAdviceCacheResponse>>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.GetAdviceCachesByIndicatorIdAsync(id, cancellationToken);
        return ToChildMutationResult(ok, notFound, errors, data, "OK", "Lấy advice cache thất bại", NotFoundMessage);
    }

    [HttpPost("{id:guid}/aliases/bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAliasResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkCreateAliases(
        Guid id,
        [FromBody] BulkCreateLabIndicatorAliasesRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorAliasResponse>>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.BulkCreateAliasesAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorAliasResponse>>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<IReadOnlyList<LabIndicatorAliasResponse>>(
                errors,
                "Bulk create aliases failed."));
        }

        return Ok(ApiResponseFactory.Success(data, $"{data.Count} alias(es) created."));
    }

    [HttpPost("{id:guid}/reference-ranges/bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorReferenceRangeResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkCreateReferenceRanges(
        Guid id,
        [FromBody] BulkCreateLabIndicatorReferenceRangesRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorReferenceRangeResponse>>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.BulkCreateReferenceRangesAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorReferenceRangeResponse>>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<IReadOnlyList<LabIndicatorReferenceRangeResponse>>(
                errors,
                "Bulk create reference ranges failed."));
        }

        return Ok(ApiResponseFactory.Success(data, $"{data.Count} reference range(s) created."));
    }

    [HttpPost("{id:guid}/advice/bulk")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabIndicatorAdviceCacheResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkCreateAdviceCaches(
        Guid id,
        [FromBody] BulkCreateLabIndicatorAdviceCachesRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorAdviceCacheResponse>>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.BulkCreateAdviceCachesAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<IReadOnlyList<LabIndicatorAdviceCacheResponse>>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<IReadOnlyList<LabIndicatorAdviceCacheResponse>>(
                errors,
                "Bulk create advice caches failed."));
        }

        return Ok(ApiResponseFactory.Success(data, $"{data.Count} advice cache entry(ies) created."));
    }

    [HttpPost("{id:guid}/aliases")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAlias(
        Guid id,
        [FromBody] CreateLabIndicatorAliasRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<LabIndicatorAliasResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.CreateAliasAsync(id, request, cancellationToken);
        return ToChildMutationResult(
            ok,
            notFound,
            errors,
            data,
            "Tạo alias thành công",
            "Tạo alias thất bại",
            NotFoundMessage);
    }

    [HttpPut("{id:guid}/aliases/{aliasId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAliasResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAlias(
        Guid id,
        Guid aliasId,
        [FromBody] UpdateLabIndicatorAliasRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<LabIndicatorAliasResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.UpdateAliasAsync(id, aliasId, request, cancellationToken);
        return ToChildMutationResult(
            ok,
            notFound,
            errors,
            data,
            "Cập nhật alias thành công",
            "Cập nhật alias thất bại",
            "Không tìm thấy alias");
    }

    [HttpDelete("{id:guid}/aliases/{aliasId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteAlias(Guid id, Guid aliasId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _labIndicatorService.SoftDeleteAliasAsync(id, aliasId, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            "Không tìm thấy alias",
            "Xóa alias thất bại",
            "Xóa alias thành công");
    }

    [HttpPost("{id:guid}/reference-ranges")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReferenceRange(
        Guid id,
        [FromBody] CreateLabIndicatorReferenceRangeRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<LabIndicatorReferenceRangeResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.CreateReferenceRangeAsync(id, request, cancellationToken);
        return ToChildMutationResult(
            ok,
            notFound,
            errors,
            data,
            "Tạo khoảng tham chiếu thành công",
            "Tạo khoảng tham chiếu thất bại",
            NotFoundMessage);
    }

    [HttpPut("{id:guid}/reference-ranges/{rangeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorReferenceRangeResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReferenceRange(
        Guid id,
        Guid rangeId,
        [FromBody] UpdateLabIndicatorReferenceRangeRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<LabIndicatorReferenceRangeResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.UpdateReferenceRangeAsync(id, rangeId, request, cancellationToken);
        return ToChildMutationResult(
            ok,
            notFound,
            errors,
            data,
            "Cập nhật khoảng tham chiếu thành công",
            "Cập nhật khoảng tham chiếu thất bại",
            "Không tìm thấy khoảng tham chiếu");
    }

    [HttpDelete("{id:guid}/reference-ranges/{rangeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteReferenceRange(Guid id, Guid rangeId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _labIndicatorService.SoftDeleteReferenceRangeAsync(id, rangeId, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            "Không tìm thấy khoảng tham chiếu",
            "Xóa khoảng tham chiếu thất bại",
            "Xóa khoảng tham chiếu thành công");
    }

    [HttpPost("{id:guid}/advice")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAdviceCache(
        Guid id,
        [FromBody] CreateLabIndicatorAdviceCacheRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<LabIndicatorAdviceCacheResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.CreateAdviceCacheAsync(id, request, cancellationToken);
        return ToChildMutationResult(
            ok,
            notFound,
            errors,
            data,
            "Tạo advice cache thành công",
            "Tạo advice cache thất bại",
            NotFoundMessage);
    }

    [HttpPut("{id:guid}/advice/{cacheId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LabIndicatorAdviceCacheResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAdviceCache(
        Guid id,
        Guid cacheId,
        [FromBody] UpdateLabIndicatorAdviceCacheRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<LabIndicatorAdviceCacheResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _labIndicatorService.UpdateAdviceCacheAsync(id, cacheId, request, cancellationToken);
        return ToChildMutationResult(
            ok,
            notFound,
            errors,
            data,
            "Cập nhật advice cache thành công",
            "Cập nhật advice cache thất bại",
            "Không tìm thấy advice cache");
    }

    [HttpDelete("{id:guid}/advice/{cacheId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteAdviceCache(Guid id, Guid cacheId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _labIndicatorService.SoftDeleteAdviceCacheAsync(id, cacheId, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            "Không tìm thấy advice cache",
            "Xóa advice cache thất bại",
            "Xóa advice cache thành công");
    }

    private IActionResult ToChildMutationResult<T>(
        bool ok,
        bool notFound,
        IEnumerable<string> errors,
        T? data,
        string successMessage,
        string failureFallbackMessage,
        string notFoundMessage)
    {
        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<T>(notFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<T>(errors, failureFallbackMessage));
        }

        return Ok(ApiResponseFactory.Success(data, successMessage));
    }
}

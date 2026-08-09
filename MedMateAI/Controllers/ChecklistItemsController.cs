using MedMateAI.Application.DTOs.ChecklistItems.Requests;
using MedMateAI.Application.DTOs.ChecklistItems.Responses;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.IService;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/checklist-items")]
public sealed class ChecklistItemsController : ControllerBase
{
    private const string InvalidIdMessage = "Id mục checklist không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy mục checklist";

    private readonly IChecklistItemService _service;

    public ChecklistItemsController(IChecklistItemService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ChecklistItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ChecklistItemResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? facilityId,
        [FromQuery] bool? isMandatory,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        if (departmentId.HasValue && departmentId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<ChecklistItemResponse>>("Id khoa không hợp lệ"));
        }

        if (facilityId.HasValue && facilityId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<ChecklistItemResponse>>("Id cơ sở không hợp lệ"));
        }

        var data = await _service.ListAsync(
            query.PageNumber,
            query.PageSize,
            departmentId,
            facilityId,
            isMandatory,
            search,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("by-department/{departmentId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChecklistItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChecklistItemResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByDepartmentId(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<ChecklistItemResponse>>("Id khoa không hợp lệ"));
        }

        var data = await _service.GetByDepartmentIdAsync(departmentId, cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("by-facility/{facilityId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChecklistItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChecklistItemResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByFacilityId(
        Guid facilityId,
        CancellationToken cancellationToken = default)
    {
        if (facilityId == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<ChecklistItemResponse>>("Id cơ sở không hợp lệ"));
        }

        var data = await _service.GetByFacilityIdAsync(facilityId, cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<ChecklistItemResponse>(InvalidIdMessage));
        }

        var data = await _service.GetByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<ChecklistItemResponse>(NotFoundMessage));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateChecklistItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ok, errors, data) = await _service.CreateAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<ChecklistItemResponse>(
                errors,
                "Tạo mục checklist thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo mục checklist thành công"));
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChecklistItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChecklistItemResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkCreate(
        [FromBody] BulkCreateChecklistItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ok, errors, data) = await _service.BulkCreateAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<IReadOnlyList<ChecklistItemResponse>>(
                errors,
                "Tạo hàng loạt mục checklist thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, $"Tạo thành công {data.Count} mục checklist"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateChecklistItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<ChecklistItemResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _service.UpdateAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<ChecklistItemResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<ChecklistItemResponse>(
                errors,
                "Cập nhật mục checklist thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật mục checklist thành công"));
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

        var (ok, notFound, errors) = await _service.SoftDeleteAsync(id, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            NotFoundMessage,
            "Xóa mục checklist thất bại",
            "Xóa mục checklist thành công");
    }
}

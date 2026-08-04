using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.MedicalFacilities.Requests;
using MedMateAI.Application.DTOs.MedicalFacilities.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/medical-facilities")]
public sealed class MedicalFacilitiesController : ControllerBase
{
    private const string InvalidIdMessage = "Id cơ sở y tế không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy cơ sở y tế";

    private readonly IMedicalFacilityService _medicalFacilityService;

    public MedicalFacilitiesController(IMedicalFacilityService medicalFacilityService)
    {
        _medicalFacilityService = medicalFacilityService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<MedicalFacilityResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var data = await _medicalFacilityService.ListMedicalFacilitiesAsync(
            query.PageNumber,
            query.PageSize,
            search,
            isActive,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalFacilityResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalFacilityResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListActive(
        [FromQuery] Guid? departmentId,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        if (departmentId.HasValue && departmentId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<IReadOnlyList<MedicalFacilityResponse>>("Id khoa không hợp lệ"));
        }

        var data = await _medicalFacilityService.ListActiveMedicalFacilitiesAsync(
            departmentId,
            search,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<MedicalFacilityResponse>(InvalidIdMessage));
        }

        var data = await _medicalFacilityService.GetMedicalFacilityByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<MedicalFacilityResponse>(NotFoundMessage));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMedicalFacilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ok, errors, data) = await _medicalFacilityService.CreateMedicalFacilityAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<MedicalFacilityResponse>(errors, "Tạo cơ sở y tế thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo cơ sở y tế thành công"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateMedicalFacilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<MedicalFacilityResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _medicalFacilityService.UpdateMedicalFacilityAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<MedicalFacilityResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<MedicalFacilityResponse>(errors, "Cập nhật cơ sở y tế thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật cơ sở y tế thành công"));
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MedicalFacilityResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateMedicalFacilityStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<MedicalFacilityResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _medicalFacilityService.UpdateMedicalFacilityStatusAsync(
            id,
            request,
            cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<MedicalFacilityResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<MedicalFacilityResponse>(errors, "Cập nhật trạng thái cơ sở y tế thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật trạng thái cơ sở y tế thành công"));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _medicalFacilityService.SoftDeleteMedicalFacilityAsync(id, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            NotFoundMessage,
            "Xóa cơ sở y tế thất bại",
            "Xóa cơ sở y tế thành công");
    }
}

using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Doctors.Requests;
using MedMateAI.Application.DTOs.Doctors.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Enums;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/doctors")]
public sealed class DoctorsController : ControllerBase
{
    private const string InvalidIdMessage = "Id bác sĩ không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy bác sĩ";

    private readonly IDoctorService _doctorService;

    public DoctorsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DoctorResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DoctorResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        [FromQuery] string? search,
        [FromQuery] Guid? facilityId,
        [FromQuery] Guid? departmentId,
        [FromQuery] bool? isActive,
        [FromQuery] DepartmentRole? departmentRole,
        CancellationToken cancellationToken = default)
    {
        if (facilityId.HasValue && facilityId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<DoctorResponse>>("Id cơ sở y tế không hợp lệ"));
        }

        if (departmentId.HasValue && departmentId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<DoctorResponse>>("Id khoa không hợp lệ"));
        }

        var data = await _doctorService.ListDoctorsAsync(
            query.PageNumber,
            query.PageSize,
            search,
            facilityId,
            departmentId,
            isActive,
            departmentRole,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DoctorResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DoctorResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListActive(
        [FromQuery] PaginationQuery query,
        [FromQuery] Guid? facilityId,
        [FromQuery] Guid? departmentId,
        [FromQuery] string? search,
        [FromQuery] DepartmentRole? departmentRole,
        CancellationToken cancellationToken = default)
    {
        if (facilityId.HasValue && facilityId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<DoctorResponse>>("Id cơ sở y tế không hợp lệ"));
        }

        if (departmentId.HasValue && departmentId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<PagedResponse<DoctorResponse>>("Id khoa không hợp lệ"));
        }

        var data = await _doctorService.ListActiveDoctorsAsync(
            query.PageNumber,
            query.PageSize,
            facilityId,
            departmentId,
            search,
            departmentRole,
            cancellationToken);

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<DoctorResponse>(InvalidIdMessage));
        }

        var data = await _doctorService.GetDoctorByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<DoctorResponse>(NotFoundMessage));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDoctorRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ok, errors, data) = await _doctorService.CreateDoctorAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<DoctorResponse>(errors, "Tạo bác sĩ thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo bác sĩ thành công"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDoctorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<DoctorResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _doctorService.UpdateDoctorAsync(
            id,
            request,
            cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<DoctorResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<DoctorResponse>(errors, "Cập nhật bác sĩ thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật bác sĩ thành công"));
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DoctorResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateDoctorStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<DoctorResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _doctorService.UpdateDoctorStatusAsync(
            id,
            request,
            cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<DoctorResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<DoctorResponse>(errors, "Cập nhật trạng thái bác sĩ thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật trạng thái bác sĩ thành công"));
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

        var (ok, notFound, errors) = await _doctorService.SoftDeleteDoctorAsync(id, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            NotFoundMessage,
            "Xóa bác sĩ thất bại",
            "Xóa bác sĩ thành công");
    }
}

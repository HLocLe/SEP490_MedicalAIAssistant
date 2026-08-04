using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.MedicalDepartments.Requests;
using MedMateAI.Application.DTOs.MedicalDepartments.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/medical-departments")]
public sealed class MedicalDepartmentsController : ControllerBase
{
    private const string InvalidIdMessage = "Id khoa không hợp lệ";
    private const string NotFoundMessage = "Không tìm thấy khoa";

    private readonly IMedicalDepartmentService _medicalDepartmentService;

    public MedicalDepartmentsController(IMedicalDepartmentService medicalDepartmentService)
    {
        _medicalDepartmentService = medicalDepartmentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalDepartmentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default)
    {
        var data = await _medicalDepartmentService.ListMedicalDepartmentsAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<MedicalDepartmentResponse>(InvalidIdMessage));
        }

        var data = await _medicalDepartmentService.GetMedicalDepartmentByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(ApiResponseFactory.Fail<MedicalDepartmentResponse>(NotFoundMessage));
        }

        return Ok(ApiResponseFactory.Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMedicalDepartmentRequest request, CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _medicalDepartmentService.CreateMedicalDepartmentAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<MedicalDepartmentResponse>(errors, "Tạo khoa thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Tạo khoa thành công"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateMedicalDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(ApiResponseFactory.Fail<MedicalDepartmentResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _medicalDepartmentService.UpdateMedicalDepartmentAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(ApiResponseFactory.Fail<MedicalDepartmentResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(ApiResponseFactory.FailFromErrors<MedicalDepartmentResponse>(errors, "Cập nhật khoa thất bại"));
        }

        return Ok(ApiResponseFactory.Success(data, "Cập nhật khoa thành công"));
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

        var (ok, notFound, errors) = await _medicalDepartmentService.SoftDeleteMedicalDepartmentAsync(id, cancellationToken);
        return ApiResponseFactory.SoftDeleteResult(
            this,
            ok,
            notFound,
            errors,
            NotFoundMessage,
            "Xóa khoa thất bại",
            "Xóa khoa thành công");
    }
}

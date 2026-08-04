using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.MedicalDepartments.Requests;
using MedMateAI.Application.DTOs.MedicalDepartments.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/medical-departments")]
public sealed class MedicalDepartmentsController : ControllerBase
{
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
        return Ok(new ApiResponse<IReadOnlyList<MedicalDepartmentResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<MedicalDepartmentResponse>
            {
                Success = false,
                Message = "Id khoa không hợp lệ",
            });
        }

        var data = await _medicalDepartmentService.GetMedicalDepartmentByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(new ApiResponse<MedicalDepartmentResponse>
            {
                Success = false,
                Message = "Không tìm thấy khoa",
            });
        }

        return Ok(new ApiResponse<MedicalDepartmentResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMedicalDepartmentRequest request, CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _medicalDepartmentService.CreateMedicalDepartmentAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse<MedicalDepartmentResponse>
            {
                Success = false,
                Message = errorList.FirstOrDefault() ?? "Tạo khoa thất bại",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<MedicalDepartmentResponse>
        {
            Success = true,
            Message = "Tạo khoa thành công",
            Data = data,
        });
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
            return BadRequest(new ApiResponse<MedicalDepartmentResponse>
            {
                Success = false,
                Message = "Id khoa không hợp lệ",
            });
        }

        var (ok, notFound, errors, data) = await _medicalDepartmentService.UpdateMedicalDepartmentAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<MedicalDepartmentResponse>
            {
                Success = false,
                Message = "Không tìm thấy khoa",
            });
        }

        if (!ok || data is null)
        {
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse<MedicalDepartmentResponse>
            {
                Success = false,
                Message = errorList.FirstOrDefault() ?? "Cập nhật khoa thất bại",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<MedicalDepartmentResponse>
        {
            Success = true,
            Message = "Cập nhật khoa thành công",
            Data = data,
        });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Id khoa không hợp lệ",
            });
        }

        var (ok, notFound, errors) = await _medicalDepartmentService.SoftDeleteMedicalDepartmentAsync(id, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = "Không tìm thấy khoa",
            });
        }

        if (!ok)
        {
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = errorList.FirstOrDefault() ?? "Xóa khoa thất bại",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Xóa khoa thành công",
        });
    }
}

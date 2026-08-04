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
        return Ok(Success(data, "OK"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Fail<MedicalDepartmentResponse>(InvalidIdMessage));
        }

        var data = await _medicalDepartmentService.GetMedicalDepartmentByIdAsync(id, cancellationToken);
        if (data is null)
        {
            return NotFound(Fail<MedicalDepartmentResponse>(NotFoundMessage));
        }

        return Ok(Success(data, "OK"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MedicalDepartmentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMedicalDepartmentRequest request, CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _medicalDepartmentService.CreateMedicalDepartmentAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(FailFromErrors<MedicalDepartmentResponse>(errors, "Tạo khoa thất bại"));
        }

        return Ok(Success(data, "Tạo khoa thành công"));
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
            return BadRequest(Fail<MedicalDepartmentResponse>(InvalidIdMessage));
        }

        var (ok, notFound, errors, data) = await _medicalDepartmentService.UpdateMedicalDepartmentAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(Fail<MedicalDepartmentResponse>(NotFoundMessage));
        }

        if (!ok || data is null)
        {
            return BadRequest(FailFromErrors<MedicalDepartmentResponse>(errors, "Cập nhật khoa thất bại"));
        }

        return Ok(Success(data, "Cập nhật khoa thành công"));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Fail(InvalidIdMessage));
        }

        var (ok, notFound, errors) = await _medicalDepartmentService.SoftDeleteMedicalDepartmentAsync(id, cancellationToken);

        if (notFound)
        {
            return NotFound(Fail(NotFoundMessage));
        }

        if (!ok)
        {
            return BadRequest(FailFromErrors(errors, "Xóa khoa thất bại"));
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Xóa khoa thành công",
        });
    }

    private static ApiResponse<T> Success<T>(T data, string message) => new()
    {
        Success = true,
        Message = message,
        Data = data,
    };

    private static ApiResponse Fail(string message) => new()
    {
        Success = false,
        Message = message,
    };

    private static ApiResponse<T> Fail<T>(string message) => new()
    {
        Success = false,
        Message = message,
    };

    private static ApiResponse FailFromErrors(IEnumerable<string> errors, string fallbackMessage)
    {
        var errorList = errors.ToList();
        return new ApiResponse
        {
            Success = false,
            Message = errorList.FirstOrDefault() ?? fallbackMessage,
            Errors = errorList,
        };
    }

    private static ApiResponse<T> FailFromErrors<T>(IEnumerable<string> errors, string fallbackMessage)
    {
        var errorList = errors.ToList();
        return new ApiResponse<T>
        {
            Success = false,
            Message = errorList.FirstOrDefault() ?? fallbackMessage,
            Errors = errorList,
        };
    }
}

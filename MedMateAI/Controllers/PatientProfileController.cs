using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.PatientProfiles.Requests;
using MedMateAI.Application.DTOs.PatientProfiles.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Authorize]
[Route("api/patient-profiles")]

public sealed class PatientProfileController : ControllerBase
{
    private const string PatientProfileNotFoundMessage = "Không tìm thấy hồ sơ bệnh nhân.";

    private readonly IPatientProfileService _patientProfileService;

    public PatientProfileController(IPatientProfileService patientProfileService)
    {
        _patientProfileService = patientProfileService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
   
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PatientProfileResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PaginationQuery query, CancellationToken cancellationToken)
    {
        var data = await _patientProfileService.ListPatientProfilesAsync(
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Ok(new ApiResponse<PagedResponse<PatientProfileResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("by-user/{userId}")]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(new ApiResponse<PatientProfileResponse>
            {
                Success = false,
                Message = "Id người dùng không hợp lệ.",
            });
        }

        var (notFound, data) = await _patientProfileService.GetPatientProfileByUserIdAsync(userId, cancellationToken);
        if (notFound || data is null)
        {
            return NotFound(new ApiResponse<PatientProfileResponse>
            {
                Success = false,
                Message = PatientProfileNotFoundMessage,
            });
        }

        return Ok(new ApiResponse<PatientProfileResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<PatientProfileResponse>
            {
                Success = false,
                Message = "Id hồ sơ bệnh nhân không hợp lệ.",
            });
        }

        var (notFound, data) = await _patientProfileService.GetPatientProfileByIdAsync(id, cancellationToken);
        if (notFound || data is null)
        {
            return NotFound(new ApiResponse<PatientProfileResponse>
            {
                Success = false,
                Message = PatientProfileNotFoundMessage,
            });
        }

        return Ok(new ApiResponse<PatientProfileResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePatientProfileRequest request, CancellationToken cancellationToken)
    {
        var (ok, errors, data) = await _patientProfileService.CreatePatientProfileAsync(request, cancellationToken);
        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<PatientProfileResponse>
            {
                Success = false,
                Message = "Tạo hồ sơ bệnh nhân thất bại.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<PatientProfileResponse>
        {
            Success = true,
            Message = "Tạo hồ sơ bệnh nhân thành công.",
            Data = data,
        });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePatientProfileRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ApiResponse<PatientProfileResponse>
            {
                Success = false,
                Message = "Id hồ sơ bệnh nhân không hợp lệ.",
            });
        }

        var (ok, notFound, errors, data) =
            await _patientProfileService.UpdatePatientProfileAsync(id, request, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse<PatientProfileResponse>
            {
                Success = false,
                Message = PatientProfileNotFoundMessage,
            });
        }

        if (!ok || data is null)
        {
            return BadRequest(new ApiResponse<PatientProfileResponse>
            {
                Success = false,
                Message = "Cập nhật hồ sơ bệnh nhân thất bại.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<PatientProfileResponse>
        {
            Success = true,
            Message = "Cập nhật hồ sơ bệnh nhân thành công.",
            Data = data,
        });
    }

    [HttpDelete("{id}")]
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
                Message = "Id hồ sơ bệnh nhân không hợp lệ.",
            });
        }

        var (ok, notFound, errors) =
            await _patientProfileService.SoftDeletePatientProfileAsync(id, cancellationToken);

        if (notFound)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = PatientProfileNotFoundMessage,
            });
        }

        if (!ok)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Xóa hồ sơ bệnh nhân thất bại.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Xóa hồ sơ bệnh nhân thành công.",
        });
    }

    

   

  
}

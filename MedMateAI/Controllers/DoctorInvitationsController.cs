using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.DoctorInvitations.Requests;
using MedMateAI.Application.DTOs.DoctorInvitations.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
public sealed class DoctorInvitationsController : ControllerBase
{
    private readonly IDoctorInvitationService _doctorInvitationService;

    public DoctorInvitationsController(IDoctorInvitationService doctorInvitationService)
    {
        _doctorInvitationService = doctorInvitationService;
    }

    [HttpGet("/api/admin/doctor-invitations")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResponse<DoctorInvitationResponse>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResponse<DoctorInvitationResponse>>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] PaginationQuery pagination,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var (succeeded, errors, data) = await _doctorInvitationService.GetAdminInvitationsAsync(
            pagination.PageNumber,
            pagination.PageSize,
            status,
            search,
            cancellationToken);

        if (!succeeded || data is null)
        {
            var errorList = errors.ToList();
            return BadRequest(new ApiResponse<PagedResponse<DoctorInvitationResponse>>
            {
                Success = false,
                Message = "List doctor invitations failed.",
                Errors = errorList,
            });
        }

        return Ok(new ApiResponse<PagedResponse<DoctorInvitationResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpPost("/api/admin/doctor-invitations")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] CreateDoctorInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new ApiResponse<DoctorInvitationResponse>
            {
                Success = false,
                Message = "Create doctor invitation failed.",
                Errors = new List<string> { "Request body is required." },
            });
        }

        try
        {
            var data = await _doctorInvitationService.CreateInvitationAsync(request, cancellationToken);
            return Ok(new ApiResponse<DoctorInvitationResponse>
            {
                Success = true,
                Message = "Doctor invitation created.",
                Data = data,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<DoctorInvitationResponse>
            {
                Success = false,
                Message = "Create doctor invitation failed.",
                Errors = new List<string> { ex.Message },
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<DoctorInvitationResponse>
            {
                Success = false,
                Message = "Create doctor invitation failed.",
                Errors = new List<string> { ex.Message },
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<DoctorInvitationResponse>
            {
                Success = false,
                Message = "Create doctor invitation failed.",
                Errors = new List<string> { ex.Message },
            });
        }
    }

    [HttpGet("/api/doctor-invitations/validate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ValidateDoctorInvitationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(
        [FromQuery] string token,
        CancellationToken cancellationToken = default)
    {
        var data = await _doctorInvitationService.ValidateInvitationAsync(
            token,
            cancellationToken);

        return Ok(new ApiResponse<ValidateDoctorInvitationResponse>
        {
            Success = data.IsValid,
            Message = data.Message,
            Data = data,
        });
    }

    [HttpPost("/api/doctor-invitations/register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RegisterDoctorByInvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RegisterDoctorByInvitationResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterDoctor(
        [FromBody] RegisterDoctorByInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new ApiResponse<RegisterDoctorByInvitationResponse>
            {
                Success = false,
                Message = "Register doctor failed.",
                Errors = new List<string> { "Request body is required." },
            });
        }

        try
        {
            var data = await _doctorInvitationService.RegisterDoctorAsync(request, cancellationToken);
            return Ok(new ApiResponse<RegisterDoctorByInvitationResponse>
            {
                Success = true,
                Message = data.Message,
                Data = data,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<RegisterDoctorByInvitationResponse>
            {
                Success = false,
                Message = "Register doctor failed.",
                Errors = new List<string> { ex.Message },
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<RegisterDoctorByInvitationResponse>
            {
                Success = false,
                Message = "Register doctor failed.",
                Errors = new List<string> { ex.Message },
            });
        }
    }

    [HttpPost("/api/admin/doctor-invitations/{id:guid}/revoke")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<DoctorInvitationResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInvitation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _doctorInvitationService.RevokeInvitationAsync(id, cancellationToken);
            if (data is null)
            {
                return NotFound(new ApiResponse<DoctorInvitationResponse>
                {
                    Success = false,
                    Message = "Doctor invitation not found.",
                });
            }

            return Ok(new ApiResponse<DoctorInvitationResponse>
            {
                Success = true,
                Message = "Doctor invitation revoked.",
                Data = data,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<DoctorInvitationResponse>
            {
                Success = false,
                Message = "Revoke doctor invitation failed.",
                Errors = new List<string> { ex.Message },
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<DoctorInvitationResponse>
            {
                Success = false,
                Message = "Revoke doctor invitation failed.",
                Errors = new List<string> { ex.Message },
            });
        }
    }
}

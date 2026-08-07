using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Users.Requests;
using MedMateAI.Application.DTOs.Users.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ApplicationUserResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var data = await _userService.GetCurrentUserAsync(cancellationToken);
        if (data is null)
        {
            return Unauthorized(new ApiResponse<ApplicationUserResponse>
            {
                Success = false,
                Message = "Unauthorized",
            });
        }

        return Ok(new ApiResponse<ApplicationUserResponse>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ApplicationUserResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var data = await _userService.ListUsersAsync(query.PageNumber, query.PageSize, cancellationToken);
        return Ok(new ApiResponse<PagedResponse<ApplicationUserResponse>>
        {
            Success = true,
            Message = "OK",
            Data = data,
        });
    }

    [HttpPut("{userId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid userId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var (ok, errors) = await _userService.UpdateUserAsync(userId, request, cancellationToken);
        if (!ok)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Update user failed.",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "User updated.",
        });
    }

    [HttpDelete("{userId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SoftDelete(Guid userId, CancellationToken cancellationToken)
    {
        var (ok, errors) = await _userService.SoftDeleteUserAsync(userId, cancellationToken);
        if (!ok)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Xóa người dùng thất bại",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Đã xóa người dùng (soft delete)",
        });
    }

    [HttpPost("{userId}/restore")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Restore(Guid userId, CancellationToken cancellationToken)
    {
        var (ok, errors) = await _userService.RestoreUserAsync(userId, cancellationToken);
        if (!ok)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Khôi phục người dùng thất bại",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Đã khôi phục người dùng",
        });
    }
}

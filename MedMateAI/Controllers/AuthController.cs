using MedMateAI.Application.DTOs.Auth.Requests;
using MedMateAI.Application.DTOs.Auth.Responses;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Users.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController]
[Route("api/authentication")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApplicationUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ApplicationUserResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var (succeeded, errorMessage, errors, result) = await _authService.RegisterAsync(request, cancellationToken);
        if (!succeeded)
        {
            return BadRequest(new ApiResponse<ApplicationUserResponse>
            {
                Success = false,
                Message = errorMessage ?? "Đăng ký thất bại",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<ApplicationUserResponse>
        {
            Success = true,
            Message = "Đăng ký thành công",
            Data = result,
        });
    }

    [HttpPost("send-register-otp")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendRegisterOtp(
        [FromBody] SendRegisterOtpRequest request,
        CancellationToken cancellationToken)
    {
        var (succeeded, errorMessage, errors) = await _authService.SendRegisterOtpAsync(request, cancellationToken);
        if (!succeeded)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = errorMessage ?? "Gửi mã OTP thất bại",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Mã OTP đã được gửi tới email của bạn",
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var (succeeded, errorMessage, result) = await _authService.LoginAsync(request, cancellationToken);
        if (!succeeded || result is null)
        {
            return Unauthorized(new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = errorMessage ?? "Invalid email or password.",
            });
        }

        return Ok(new ApiResponse<AuthResponse>
        {
            Success = true,
            Message = "Login succeeded",
            Data = result,
        });
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Google([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var (succeeded, errorMessage, errors, result) = await _authService.LoginWithGoogleAsync(request, cancellationToken);
        if (!succeeded || result is null)
        {
            var message = errorMessage ?? "Google login failed";
            var unauthorized =
                message is "Credential Google không hợp lệ"
                    or "Tài khoản của bạn đã bị lock"
                    or "Tài khoản đã bị xóa";

            if (unauthorized)
            {
                return Unauthorized(new ApiResponse<AuthResponse>
                {
                    Success = false,
                    Message = message,
                    Errors = errors.ToList(),
                });
            }

            return BadRequest(new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = message,
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse<AuthResponse>
        {
            Success = true,
            Message = "Login succeeded",
            Data = result,
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var (succeeded, result) = await _authService.RefreshAccessTokenAsync(cancellationToken);
        if (!succeeded || result is null)
        {
            return Unauthorized(new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = "Refresh token missing, invalid, or expired.",
            });
        }

        return Ok(new ApiResponse<AuthResponse>
        {
            Success = true,
            Message = "Token refreshed.",
            Data = result,
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(cancellationToken);
        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Logged out.",
        });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var (succeeded, errorMessage, errors) = await _authService.ForgotPasswordAsync(request, cancellationToken);
        if (!succeeded)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = errorMessage ?? "Yêu cầu đặt lại mật khẩu thất bại",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Nếu email tồn tại trên hệ thống, mã OTP đã được gửi.",
        });
    }

    [HttpPost("change-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePasswordWithOtp([FromBody] ChangePasswordWithOtpRequest request, CancellationToken cancellationToken)
    {
        var (succeeded, errorMessage, errors) = await _authService.ChangePasswordWithOtpAsync(request, cancellationToken);
        if (!succeeded)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = errorMessage ?? "Thay đổi mật khẩu thất bại",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Mật khẩu đã được thay đổi thành công.",
        });
    }

    [HttpPost("update-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var (succeeded, errorMessage, errors) = await _authService.ChangePasswordAsync(request, cancellationToken);
        if (!succeeded)
        {
            if (errorMessage is "Unauthorized")
            {
                return Unauthorized(new ApiResponse
                {
                    Success = false,
                    Message = errorMessage,
                    Errors = errors.ToList(),
                });
            }

            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = errorMessage ?? "Đổi mật khẩu thất bại",
                Errors = errors.ToList(),
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Mật khẩu đã được thay đổi thành công.",
        });
    }
}


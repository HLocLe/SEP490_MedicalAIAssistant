using MedMateAI.Application.DTOs.Auth.Requests;
using MedMateAI.Application.DTOs.Auth.Responses;
using MedMateAI.Application.DTOs.Users.Responses;

namespace MedMateAI.Application.IService;

public interface IAuthService
{
    Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors, ApplicationUserResponse? Result)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, AuthResponse? Result)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

   
    Task<(bool Succeeded, AuthResponse? Result)> RefreshAccessTokenAsync(
        CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors, AuthResponse? Result)> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors)> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors)> ChangePasswordWithOtpAsync(
        ChangePasswordWithOtpRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors)> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}

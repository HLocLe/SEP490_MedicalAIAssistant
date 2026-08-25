using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Users.Requests;
using MedMateAI.Application.DTOs.Users.Responses;

namespace MedMateAI.Application.IService;

public interface IUserService
{
    Task<ApplicationUserResponse?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<ApplicationUserResponse?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApplicationUserResponse?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PagedResponse<ApplicationUserResponse>> ListUsersAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<(
        bool Succeeded,
        bool NotFound,
        IEnumerable<string> Errors,
        ApplicationUserResponse? Data)> UpdateMyProfileAsync(
            Guid userId,
            UpdateMyProfileRequest request,
            CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateCurrentUserPhoneAsync(
        Guid userId,
        string phoneNumber,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors)> SoftDeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors)> RestoreUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors)> MarkPatientProfileCompletedAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

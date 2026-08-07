using MedMateAI.Application.IService;
using MedMateAI.Domain.Enums;
using MedMateAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Auth.Services;

public sealed class DoctorAccountRegistrationService : IDoctorAccountRegistrationService
{
    private const string DoctorRoleName = "Doctor";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public DoctorAccountRegistrationService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var normalizedEmail = _userManager.NormalizeEmail(email.Trim());
        return await _userManager.Users.AnyAsync(
            x => x.NormalizedEmail == normalizedEmail && !x.IsDeleted,
            cancellationToken);
    }

    public async Task<(bool Succeeded, Guid UserId, IEnumerable<string> Errors)> CreateDoctorUserAsync(
        string email,
        string fullName,
        string password,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, Guid.Empty, new[] { "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (false, Guid.Empty, new[] { "FullName is required." });
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, Guid.Empty, new[] { "Password is required." });
        }

        if (await EmailExistsAsync(email, cancellationToken))
        {
            return (false, Guid.Empty, new[] { "This email is already registered." });
        }

        if (!await _roleManager.RoleExistsAsync(DoctorRoleName))
        {
            var createRoleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(DoctorRoleName));
            if (!createRoleResult.Succeeded)
            {
                return (false, Guid.Empty, createRoleResult.Errors.Select(x => x.Description));
            }
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            DisplayName = fullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            Status = UserStatus.Confirmed,
            IsFirstLogin = true,
            IsProfileCompleted = true,
            CreatedAt = DateTime.UtcNow,
        };

        var createUserResult = await _userManager.CreateAsync(user, password);
        if (!createUserResult.Succeeded)
        {
            return (false, Guid.Empty, createUserResult.Errors.Select(x => x.Description));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, DoctorRoleName);
        if (!addRoleResult.Succeeded)
        {
            return (false, user.Id, addRoleResult.Errors.Select(x => x.Description));
        }

        return (true, user.Id, Array.Empty<string>());
    }
}

using System.Security.Claims;
using AutoMapper;
using MedMateAI.Application.Common.Time;
using MedMateAI.Application.Common.Validation;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Users.Requests;
using MedMateAI.Application.IService;
using MedMateAI.Application.DTOs.Users.Responses;
using MedMateAI.Domain.Repository;
using MedMateAI.Infrastructure;
using MedMateAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Auth.Services;

public sealed class UserService : IUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGenericRepository<ApplicationUser> _userRepository;
    private readonly IMapper _mapper;
    private readonly ApplicationDbContext _db;
    private readonly IRecoveryPlanRealtimeNotifier _realtimeNotifier;

    public UserService(
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager,
        IGenericRepository<ApplicationUser> userRepository,
        IMapper mapper,
        ApplicationDbContext db,
        IRecoveryPlanRealtimeNotifier realtimeNotifier)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _userRepository = userRepository;
        _mapper = mapper;
        _db = db;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ApplicationUserResponse?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            return null;
        }

        var appUser = await _userManager.FindByIdAsync(id.ToString());
        if (appUser is null || appUser.IsDeleted)
        {
            return null;
        }

        var dto = _mapper.Map<ApplicationUserResponse>(appUser);
        var roles = await _userManager.GetRolesAsync(appUser);
        dto.Roles = roles.ToArray();
        return dto;
    }

    public async Task<ApplicationUserResponse?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser is null || appUser.IsDeleted)
        {
            return null;
        }

        return _mapper.Map<ApplicationUserResponse>(appUser);
    }

    public async Task<ApplicationUserResponse?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = _userManager.NormalizeEmail(email.Trim());
        var appUser = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized && !u.IsDeleted, cancellationToken);

        return appUser is null ? null : _mapper.Map<ApplicationUserResponse>(appUser);
    }

    public async Task<bool> UserExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = _userManager.NormalizeEmail(email.Trim());
        return await _userManager.Users.AnyAsync(
            u => u.NormalizedEmail == normalized && !u.IsDeleted,
            cancellationToken);
    }

    public async Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        return appUser is not null && !appUser.IsDeleted && await _userManager.IsInRoleAsync(appUser, role);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser is null || appUser.IsDeleted)
        {
            return Array.Empty<string>();
        }

        var roles = await _userManager.GetRolesAsync(appUser);
        return roles.ToArray();
    }

    public async Task<PagedResponse<ApplicationUserResponse>> ListUsersAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paged = await _userRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            orderBy: q => q.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id),
            cancellationToken: cancellationToken);

        var rolesByUserId = await GetRoleNamesByUserIdsAsync(paged.Items, cancellationToken);

        var items = new List<ApplicationUserResponse>(paged.Items.Count);
        foreach (var u in paged.Items)
        {
            var dto = _mapper.Map<ApplicationUserResponse>(u);
            dto.Roles = rolesByUserId.TryGetValue(u.Id, out var names) ? names : Array.Empty<string>();
            items.Add(dto);
        }

        return new PagedResponse<ApplicationUserResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = items,
        };
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, new[] { "Invalid user id." });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        var dateOfBirthValidation = DateOfBirthValidationPolicy.ValidateForProfileUpdate(
            request.DateOfBirth,
            VietnamBusinessDate.GetToday(DateTimeOffset.UtcNow));
        if (!dateOfBirthValidation.IsValid)
        {
            return (false, new[] { dateOfBirthValidation.ErrorMessage! });
        }

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (request.Address is not null)
        {
            user.Address = request.Address;
        }

        if (request.Gender.HasValue)
        {
            user.Gender = request.Gender;
        }

        if (request.DateOfBirth.HasValue)
        {
            user.DateOfBirth = request.DateOfBirth;
        }

        if (request.PhoneNumber is not null)
        {
            user.PhoneNumber = request.PhoneNumber;
        }

        var updateResult = await _userManager.UpdateAsync(user);
        return updateResult.Succeeded
            ? (true, Array.Empty<string>())
            : (false, updateResult.Errors.Select(e => e.Description));
    }

    public async Task<(
        bool Succeeded,
        bool NotFound,
        IEnumerable<string> Errors,
        ApplicationUserResponse? Data)> UpdateMyProfileAsync(
            Guid userId,
            UpdateMyProfileRequest request,
            CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, false, new[] { "Invalid user id." }, null);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            return (false, true, new[] { "User not found." }, null);
        }

        var dateOfBirthValidation = DateOfBirthValidationPolicy.ValidateRequired(
            request.DateOfBirth,
            VietnamBusinessDate.GetToday(DateTimeOffset.UtcNow));
        if (!dateOfBirthValidation.IsValid || !request.DateOfBirth.HasValue)
        {
            return (
                false,
                false,
                new[] { dateOfBirthValidation.ErrorMessage! },
                null);
        }

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length > 256)
        {
            return (
                false,
                false,
                new[] { "Display name must not exceed 256 characters." },
                null);
        }

        var address = string.IsNullOrWhiteSpace(request.Address)
            ? null
            : request.Address.Trim();
        if (address?.Length > 512)
        {
            return (
                false,
                false,
                new[] { "Address must not exceed 512 characters." },
                null);
        }

        if (request.Gender.HasValue && !Enum.IsDefined(request.Gender.Value))
        {
            return (false, false, new[] { "Gender is invalid." }, null);
        }

        user.DisplayName = displayName;
        user.Address = address;
        user.Gender = request.Gender;
        user.DateOfBirth = request.DateOfBirth.Value;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return (
                false,
                false,
                updateResult.Errors.Select(error => error.Description),
                null);
        }

        var response = _mapper.Map<ApplicationUserResponse>(user);
        var roles = await _userManager.GetRolesAsync(user);
        response.Roles = roles.ToArray();

        return (true, false, Array.Empty<string>(), response);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateCurrentUserPhoneAsync(
        Guid userId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, new[] { "Invalid user id." });
        }

        var normalizedPhone = phoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return (false, new[] { "Số điện thoại là bắt buộc." });
        }

        var digitsOnly = new string(normalizedPhone.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 9 || digitsOnly.Length > 15)
        {
            return (false, new[] { "Số điện thoại không hợp lệ." });
        }

        return await UpdateUserAsync(
            userId,
            new UpdateUserRequest { PhoneNumber = normalizedPhone },
            cancellationToken);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> SoftDeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, new[] { "Id người dùng không hợp lệ" });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, new[] { "Không tìm thấy người dùng" });
        }

        if (user.IsDeleted)
        {
            return (false, new[] { "Người dùng đã bị xóa" });
        }

        var hasLinkedDoctor = await HasLinkedDoctorAsync(user.Id, cancellationToken);
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return (false, updateResult.Errors.Select(e => e.Description));
        }

        await _db.RefreshTokens
            .Where(x => x.UserId == user.Id && !x.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.IsRevoked, true)
                    .SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddDays(-1)),
                cancellationToken);

        if (hasLinkedDoctor)
        {
            await _realtimeNotifier.TryNotifyDoctorRealtimeAccessChangedAsync(
                user.Id,
                user.DeletedAt.Value,
                CancellationToken.None);
        }

        return (true, Array.Empty<string>());
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> RestoreUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, new[] { "Id người dùng không hợp lệ" });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, new[] { "Không tìm thấy người dùng" });
        }

        if (!user.IsDeleted)
        {
            return (false, new[] { "Người dùng chưa bị xóa" });
        }

        user.IsDeleted = false;
        user.DeletedAt = null;

        var updateResult = await _userManager.UpdateAsync(user);
        return updateResult.Succeeded
            ? (true, Array.Empty<string>())
            : (false, updateResult.Errors.Select(e => e.Description));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> MarkPatientProfileCompletedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, new[] { " user id is required" });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            return (false, new[] { "User not found." });
        }

        var dateOfBirthValidation = DateOfBirthValidationPolicy.ValidateRequired(
            user.DateOfBirth,
            VietnamBusinessDate.GetToday(DateTimeOffset.UtcNow));
        if (!dateOfBirthValidation.IsValid)
        {
            return (false, new[] { dateOfBirthValidation.ErrorMessage! });
        }

        if (user.IsProfileCompleted)
        {
            return (true, Array.Empty<string>());
        }

        user.IsProfileCompleted = true;
        
        var updateResult = await _userManager.UpdateAsync(user);
        return updateResult.Succeeded
            ? (true, Array.Empty<string>())
            : (false, updateResult.Errors.Select(e => e.Description));
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetRoleNamesByUserIdsAsync(
        IReadOnlyList<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var userIds = users.Select(u => u.Id).Distinct().ToList();

        var rows = await (
            from ur in _db.UserRoles.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, r.Name }
        ).ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(x => x.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
    }

    private Task<bool> HasLinkedDoctorAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _db.Doctors
            .AsNoTracking()
            .AnyAsync(
                doctor => doctor.UserId == userId && !doctor.IsDeleted,
                cancellationToken);
    }
}

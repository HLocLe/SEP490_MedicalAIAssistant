using System.Security.Claims;
using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Users.Requests;
using MedMateAI.Application.IService;
using MedMateAI.Application.DTOs.Users.Responses;
using MedMateAI.Domain.Enums;
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

    public UserService(
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager,
        IGenericRepository<ApplicationUser> userRepository,
        IMapper mapper,
        ApplicationDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _userRepository = userRepository;
        _mapper = mapper;
        _db = db;
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
            orderBy: q => q.OrderBy(u => u.Email),
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

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> SoftDeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, new[] { "Invalid user id." });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, new[] { "User not found." });
        }

        if (user.IsDeleted)
        {
            return (false, new[] { "User is already deleted." });
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        return updateResult.Succeeded
            ? (true, Array.Empty<string>())
            : (false, updateResult.Errors.Select(e => e.Description));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, ApproveUserResponse? DataData)> ApproveUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, new[] { "Invalid user id." }, null);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, new[] { "User not found." }, null);
        }

        if (user.IsDeleted)
        {
            return (false, new[] { "Cannot approve a deleted user." }, null);
        }

        if (user.Status != UserStatus.Pending)
        {
            return (false, new[] { "Only pending accounts can be approved." }, null);
        }

        user.Status = UserStatus.Confirmed;
        var updateResult = await _userManager.UpdateAsync(user);
        return updateResult.Succeeded
            ? (true, Array.Empty<string>(), new ApproveUserResponse { UserId = user.Id, Status = user.Status })
            : (false, updateResult.Errors.Select(e => e.Description), null);
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
}

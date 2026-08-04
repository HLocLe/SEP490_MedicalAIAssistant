using MedMateAI.Application.DTOs.Auth.Requests;
using MedMateAI.Application.DTOs.Auth.Responses;
using MedMateAI.Application.DTOs.Users.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.Auth.Providers;
using MedMateAI.Infrastructure.Auth.Security;
using MedMateAI.Infrastructure.Identity;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MedMateAI.Domain.Enums;
using AutoMapper;

namespace MedMateAI.Infrastructure.Auth.Services;

public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan PasswordResetOtpLifetime = TimeSpan.FromMinutes(1);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IMemoryCache _cache;
    private readonly IEmailOtpSender _emailOtpSender;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IMemoryCache cache,
        IEmailOtpSender emailOtpSender,
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext db,
        IConfiguration configuration,
        IMapper mapper)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _cache = cache;
        _emailOtpSender = emailOtpSender;
        _httpContextAccessor = httpContextAccessor;
        _db = db;
        _configuration = configuration;
        _mapper = mapper;
    }
    
    //
    public async Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors, ApplicationUserResponse? Result)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var (succeeded, errorMessage, errors, user) = await CreateRegisteredUserAsync(
            request,
            UserStatus.Confirmed,
            "User");

        if (!succeeded || user is null)
        {
            return (false, errorMessage, errors, null);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var result = _mapper.Map<ApplicationUserResponse>(user);
        result.Roles = roles.ToList();

        return (true, null, Array.Empty<string>(), result);
    }

    private async Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors, ApplicationUser? User)> CreateRegisteredUserAsync(
        RegisterRequest request,
        UserStatus status,
        string role)
    {
        if (!string.Equals(request.Password, request.confirmPassword, StringComparison.Ordinal))
        {
            const string message = "Mật khẩu xác nhận không khớp";
            return (false, message, new[] { message }, null);
        }

        var userName = string.IsNullOrWhiteSpace(request.UserName) ? request.Email : request.UserName;

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Address = request.Address,
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            Status = status,
            IsFirstLogin = true,
        };

        var identityResult = await _userManager.CreateAsync(user, request.Password);
        if (!identityResult.Succeeded)
        {
            var (errorMessage, errors) = MapIdentityRegisterErrors(identityResult.Errors);
            return (false, errorMessage, errors, null);
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, role);
        if (!addRoleResult.Succeeded)
        {
            var errors = addRoleResult.Errors.Select(e => e.Description).ToArray();
            return (false, null, errors, null);
        }

        return (true, null, Array.Empty<string>(), user);
    }

    //
    public async Task<(bool Succeeded, string? ErrorMessage, AuthResponse? Result)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return (false, null, null);
        }

        if (user.Status == UserStatus.Pending)
        {
            return (false, null, null);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return (false, "Tài khoản của bạn đã bị lock", null);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return (false, null, null);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var result = await GenerateAuthResponseAsync(user, cancellationToken);
        return (true, null, result);
    }

    //
    public async Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors, AuthResponse? Result)> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Credential))
        {
            const string message = "Credential Google là bắt buộc";
            return (false, message, new[] { message }, null);
        }

        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            const string message = "Google chưa được cấu hình";
            return (false, message, new[] { message }, null);
        }

        var credential = request.Credential.Trim().Trim('"');

        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                credential,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId },
                });
        }
        catch
        {
            const string message = "Credential Google không hợp lệ";
            return (false, message, new[] { message }, null);
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            const string message = "Tài khoản Google không có email";
            return (false, message, new[] { message }, null);
        }

        var email = payload.Email.Trim();

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = payload.Name,
                Status = UserStatus.Confirmed,
                IsFirstLogin = true,
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return (false, null, createResult.Errors.Select(e => e.Description), null);
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!addRoleResult.Succeeded)
            {
                return (false, null, addRoleResult.Errors.Select(e => e.Description), null);
            }
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return (false, "Tài khoản của bạn đã bị lock", Array.Empty<string>(), null);
        }

        var result = await GenerateAuthResponseAsync(user, cancellationToken);
        return (true, null, Array.Empty<string>(), result);
    }
    
    //
    public async Task<(bool Succeeded, AuthResponse? Result)> RefreshAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null ||
            !httpContext.Request.Cookies.TryGetValue("refreshToken", out var refreshTokenRaw) ||
            string.IsNullOrWhiteSpace(refreshTokenRaw))
        {
            return (false, null);
        }

        var hash = RefreshTokenHasher.Sha256Hex(refreshTokenRaw.Trim());

        var utcNow = DateTime.UtcNow;

        var existing = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.Token == hash && !x.IsRevoked && !x.IsUsed && x.ExpiresAt > utcNow,
                cancellationToken);

        if (existing is null)
        {
            var reusedOrInvalid = await _db.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == hash, cancellationToken);

            if (reusedOrInvalid is not null)
            {
                await RevokeAllRefreshTokensForUserAsync(reusedOrInvalid.UserId, cancellationToken);
                ClearRefreshTokenCookie(httpContext);
            }

            return (false, null);
        }

        var user = existing.User;
        if (await _userManager.IsLockedOutAsync(user))
        {
            return (false, null);
        }

        var roles = await _userManager.GetRolesAsync(user);

        var (accessToken, accessExpires) = _jwtTokenGenerator.CreateAccessToken(
            user.Id.ToString(),
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.ToArray());

        existing.IsUsed = true;

        var (newRefreshToken, refreshExpires) = _jwtTokenGenerator.CreateRefreshToken();
        var newRefreshHash = RefreshTokenHasher.Sha256Hex(newRefreshToken);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = newRefreshHash,
            UserId = user.Id,
            ExpiresAt = refreshExpires.UtcDateTime,
            IsUsed = false,
            IsRevoked = false,
            AddedDate = utcNow,
        });

        await _db.SaveChangesAsync(cancellationToken);

        httpContext.Response.Cookies.Append(
            "refreshToken",
            newRefreshToken,
            CreateRefreshTokenCookieOptions(refreshExpires));

        return (true, new AuthResponse
        {
            AccessToken = accessToken,
            Email = user.Email ?? string.Empty,
            UserId = user.Id,
            Roles = roles.ToArray(),
            ExpiresAtUtc = accessExpires,
            FirstLogin = user.IsFirstLogin,
        });
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null &&
            httpContext.Request.Cookies.TryGetValue("refreshToken", out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = RefreshTokenHasher.Sha256Hex(refreshToken.Trim());
            var existing = await _db.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == hash, cancellationToken);

            if (existing is not null)
            {
                var user = await _db.Set<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.Id == existing.UserId, cancellationToken);
                if (user is not null && user.IsFirstLogin)
                {
                    user.IsFirstLogin = false;
                }

                existing.IsRevoked = true;
                existing.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1).UtcDateTime;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        if (httpContext is not null)
        {
            ClearRefreshTokenCookie(httpContext);
        }
    }

    //
    public async Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors)> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            const string message = "Email là bắt buộc";
            return (false, message, new[] { message });
        }

        var email = request.Email.Trim();

        var cacheKey = GetPasswordResetCacheKey(email);

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return (true, null, Array.Empty<string>());
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        var (sent, otp) = await _emailOtpSender.SendOtpEmailAsync(email, cancellationToken);
        if (!sent || otp is null)
        {
            const string message = "Không thể gửi email OTP. Vui lòng thử lại sau.";
            return (false, message, new[] { message });
        }

        var entry = new PasswordResetOtpCacheEntry
        {
            Otp = otp.Trim(),
            ResetToken = resetToken,
        };

        _cache.Set(cacheKey, entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PasswordResetOtpLifetime,
        });

        return (true, null, Array.Empty<string>());
    }

    //
    public async Task<(bool Succeeded, string? ErrorMessage, IEnumerable<string> Errors)> ChangePasswordWithOtpAsync(
        ChangePasswordWithOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            const string message = "Email là bắt buộc";
            return (false, message, new[] { message });
        }

        if (string.IsNullOrWhiteSpace(request.Otp))
        {
            const string message = "Mã OTP là bắt buộc";
            return (false, message, new[] { message });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            const string message = "Mật khẩu mới là bắt buộc";
            return (false, message, new[] { message });
        }

        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            const string message = "Mật khẩu xác nhận không khớp";
            return (false, message, new[] { message });
        }

        var cacheKey = GetPasswordResetCacheKey(request.Email);

        if (!_cache.TryGetValue(cacheKey, out PasswordResetOtpCacheEntry? entry) || entry is null)
        {
            const string message = "Mã OTP không hợp lệ hoặc đã hết hạn";
            return (false, message, new[] { message });
        }

        if (!string.Equals(entry.Otp, request.Otp.Trim(), StringComparison.Ordinal))
        {
            const string message = "Mã OTP không hợp lệ hoặc đã hết hạn";
            return (false, message, new[] { message });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            const string message = "Mã OTP không hợp lệ hoặc đã hết hạn";
            return (false, message, new[] { message });
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, entry.ResetToken, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            const string message = "Đặt lại mật khẩu thất bại";
            return (false, message, resetResult.Errors.Select(e => e.Description));
        }

        _cache.Remove(cacheKey);
        return (true, null, Array.Empty<string>());
    }

    //
    private async Task<AuthResponse> GenerateAuthResponseAsync(
    ApplicationUser user,
    CancellationToken cancellationToken)
    {
       
        var roles = await _userManager.GetRolesAsync(user);

        var (token, expires) = _jwtTokenGenerator.CreateAccessToken(
            user.Id.ToString(),
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.ToArray());

       
        var (refreshToken, refreshExpires) = _jwtTokenGenerator.CreateRefreshToken();
        var refreshTokenHash = RefreshTokenHasher.Sha256Hex(refreshToken);

       
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshTokenHash,
            UserId = user.Id,
            ExpiresAt = refreshExpires.UtcDateTime,
            IsUsed = false,
            IsRevoked = false,
            AddedDate = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);

       
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null)
        {
            httpContext.Response.Cookies.Append(
                "refreshToken",
                refreshToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = refreshExpires,
                    Path = "/",
                });
        }

        return new AuthResponse
        {
            AccessToken = token,
            Email = user.Email ?? string.Empty,
            UserId = user.Id,
            Roles = roles.ToArray(),
            ExpiresAtUtc = expires,
            FirstLogin = user.IsFirstLogin,
            IsProfileCompleted=user.IsProfileCompleted,
        };
    }

    private static CookieOptions CreateRefreshTokenCookieOptions(DateTimeOffset expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = expires,
            Path = "/",
        };
    }

    private static void ClearRefreshTokenCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Append(
            "refreshToken",
            string.Empty,
            CreateRefreshTokenCookieOptions(DateTimeOffset.UtcNow.AddDays(-1)));
    }

        private async Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            await _db.RefreshTokens
                .Where(x => x.UserId == userId && !x.IsRevoked)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(t => t.IsRevoked, true)
                        .SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddDays(-1)),
                    cancellationToken);
        }

    //
    private static string GetPasswordResetCacheKey(string email)
        => $"pwdreset:{email.Trim().ToLowerInvariant()}";

    private static (string? ErrorMessage, IReadOnlyList<string> Errors) MapIdentityRegisterErrors(
        IEnumerable<IdentityError> identityErrors)
    {
        var errors = identityErrors.ToList();
        var descriptions = errors.Select(e => e.Description).ToArray();

        if (errors.Any(e => e.Code == "DuplicateEmail"))
        {
            const string message = "Email đã tồn tại";
            return (message, new[] { message });
        }

        if (errors.Any(e => e.Code == "DuplicateUserName"))
        {
            const string message = "Username đã được sử dụng";
            return (message, new[] { message });
        }

        return (null, descriptions);
    }
  
    // 
    private sealed class PasswordResetOtpCacheEntry
    {
        public required string Otp { get; init; }

        public required string ResetToken { get; init; }
    }
}


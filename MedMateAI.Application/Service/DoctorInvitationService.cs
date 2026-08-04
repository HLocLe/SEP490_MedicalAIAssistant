using System.Net.Mail;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MedMateAI.Application.DTOs.DoctorInvitations.Requests;
using MedMateAI.Application.DTOs.DoctorInvitations.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MedMateAI.Application.Service;

public sealed class DoctorInvitationService : IDoctorInvitationService
{
    private const int InvitationLifetimeMinutes = 10;
    private const string DoctorRoleName = "Doctor";
    private const string InvitationSubject = "Lời mời đăng ký tài khoản bác sĩ - MedicalMateAI";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailSender _emailSender;
    private readonly IInvitationTokenService _tokenService;
    private readonly IDoctorAccountRegistrationService _doctorAccountRegistrationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly FrontendOptions _frontendOptions;

    public DoctorInvitationService(
        IUnitOfWork unitOfWork,
        IEmailSender emailSender,
        IInvitationTokenService tokenService,
        IDoctorAccountRegistrationService doctorAccountRegistrationService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<FrontendOptions> frontendOptions)
    {
        _unitOfWork = unitOfWork;
        _emailSender = emailSender;
        _tokenService = tokenService;
        _doctorAccountRegistrationService = doctorAccountRegistrationService;
        _httpContextAccessor = httpContextAccessor;
        _frontendOptions = frontendOptions.Value;
    }

    public async Task<DoctorInvitationResponse> CreateInvitationAsync(
        CreateDoctorInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentException("Request body is required.");
        }

        var email = NormalizeEmail(request.Email);

        if (await _doctorAccountRegistrationService.EmailExistsAsync(email, cancellationToken))
        {
            throw new InvalidOperationException("This email is already registered.");
        }

        Doctor? linkedDoctor = null;
        if (request.DoctorId.HasValue)
        {
            if (request.DoctorId.Value == Guid.Empty)
            {
                throw new ArgumentException("DoctorId is invalid.");
            }

            linkedDoctor = await _unitOfWork.Doctors.GetByIdAsync(
                request.DoctorId.Value,
                cancellationToken);

            if (linkedDoctor is null || linkedDoctor.IsDeleted)
            {
                throw new KeyNotFoundException("Doctor profile not found.");
            }

            if (linkedDoctor.UserId.HasValue)
            {
                throw new InvalidOperationException("This doctor profile is already linked to a user account.");
            }

            var activeDoctorInvitation = await _unitOfWork.DoctorInvitations.GetPendingByDoctorIdAsync(
                request.DoctorId.Value,
                cancellationToken);

            if (activeDoctorInvitation is not null)
            {
                throw new InvalidOperationException("An active invitation already exists for this doctor profile.");
            }
        }

        var activeInvitation = await _unitOfWork.DoctorInvitations.GetPendingByEmailAsync(
            email,
            cancellationToken);

        if (activeInvitation is not null)
        {
            throw new InvalidOperationException("An active invitation already exists for this email.");
        }

        var rawToken = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(rawToken);
        var utcNow = DateTime.UtcNow;

        var invitation = new DoctorInvitation
        {
            Id = Guid.NewGuid(),
            Email = email,
            TokenHash = tokenHash,
            DoctorId = request.DoctorId,
            ExpiresAt = utcNow.AddMinutes(InvitationLifetimeMinutes),
            CreatedByAdminId = GetCurrentUserId(),
            Status = DoctorInvitationStatus.Pending,
            CreatedAt = utcNow,
        };

        _unitOfWork.DoctorInvitations.Add(invitation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var registerUrl = BuildRegisterUrl(rawToken);
        var htmlContent = BuildInvitationEmailHtml(registerUrl);

        try
        {
            await _emailSender.SendAsync(
                email,
                InvitationSubject,
                htmlContent,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var revokedAt = DateTime.UtcNow;
            invitation.Status = DoctorInvitationStatus.Revoked;
            invitation.RevokedAt = revokedAt;
            invitation.UpdatedAt = revokedAt;

            _unitOfWork.DoctorInvitations.Update(invitation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new InvalidOperationException("Failed to send invitation email.", ex);
        }

        return MapToResponse(invitation, linkedDoctor);
    }

    public async Task<ValidateDoctorInvitationResponse> ValidateInvitationAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var (_, response) = await ValidateInvitationInternalAsync(
            token,
            markExpired: true,
            cancellationToken);

        return response;
    }

    public async Task<RegisterDoctorByInvitationResponse> RegisterDoctorAsync(
        RegisterDoctorByInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentException("Request body is required.");
        }

        var errors = ValidateRegisterRequest(request);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        var (invitation, validation) = await ValidateInvitationInternalAsync(
            request.Token,
            markExpired: true,
            cancellationToken);

        if (!validation.IsValid || invitation is null)
        {
            throw new InvalidOperationException(validation.Message);
        }

        if (await _doctorAccountRegistrationService.EmailExistsAsync(invitation.Email, cancellationToken))
        {
            throw new InvalidOperationException("This email is already registered.");
        }

        var fullName = NormalizeText(request.FullName);
        var linkedDoctor = invitation.Doctor;
        var existingDoctorId = invitation.DoctorId;
        var hasExistingDoctorProfile = existingDoctorId.HasValue;

        if (hasExistingDoctorProfile)
        {
            linkedDoctor ??= await _unitOfWork.Doctors.GetByIdAsync(
                existingDoctorId.GetValueOrDefault(),
                cancellationToken);

            if (linkedDoctor is null || linkedDoctor.IsDeleted)
            {
                throw new InvalidOperationException("Doctor profile is no longer available.");
            }

            if (linkedDoctor.UserId.HasValue)
            {
                throw new InvalidOperationException("This doctor profile is already linked to a user account.");
            }

            fullName ??= NormalizeText(linkedDoctor.FullName);

            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException("FullName is required.");
            }

            if (request.FacilityDepartmentId.HasValue)
            {
                var facilityDepartmentIsValid = await IsValidFacilityDepartmentAsync(
                    request.FacilityDepartmentId.Value,
                    cancellationToken);

                if (!facilityDepartmentIsValid)
                {
                    throw new ArgumentException("FacilityDepartmentId is invalid, deleted, or belongs to inactive facility.");
                }
            }
        }
        else
        {
            var newProfileErrors = ValidateNewDoctorProfileRequest(request, fullName);
            if (newProfileErrors.Count > 0)
            {
                throw new ArgumentException(string.Join(" ", newProfileErrors));
            }

            var facilityDepartmentIsValid = await IsValidFacilityDepartmentAsync(
                request.FacilityDepartmentId.GetValueOrDefault(),
                cancellationToken);

            if (!facilityDepartmentIsValid)
            {
                throw new ArgumentException("FacilityDepartmentId is invalid, deleted, or belongs to inactive facility.");
            }
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var createUserResult = await _doctorAccountRegistrationService.CreateDoctorUserAsync(
                invitation.Email,
                fullName!,
                request.Password,
                NormalizeText(request.PhoneNumber),
                cancellationToken);

            if (!createUserResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", createUserResult.Errors));
            }

            var utcNow = DateTime.UtcNow;
            Guid doctorId;

            if (hasExistingDoctorProfile)
            {
                linkedDoctor!.UserId = createUserResult.UserId;

                if (string.IsNullOrWhiteSpace(linkedDoctor.FullName))
                {
                    linkedDoctor.FullName = fullName;
                }

                var qualification = NormalizeText(request.Qualification);
                if (!string.IsNullOrWhiteSpace(qualification)
                    && string.IsNullOrWhiteSpace(linkedDoctor.Specialty))
                {
                    linkedDoctor.Specialty = qualification;
                }

                if (request.YearsOfExperience.HasValue && !linkedDoctor.YearsOfExperience.HasValue)
                {
                    linkedDoctor.YearsOfExperience = request.YearsOfExperience.Value;
                }

                if (request.FacilityDepartmentId.HasValue)
                {
                    linkedDoctor.FacilityDepartmentId = request.FacilityDepartmentId.Value;
                }

                if (request.DepartmentRole.HasValue)
                {
                    linkedDoctor.DepartmentRole = request.DepartmentRole.Value;
                }

                linkedDoctor.UpdatedAt = utcNow;
                doctorId = linkedDoctor.Id;
                _unitOfWork.Doctors.Update(linkedDoctor);
            }
            else
            {
                var doctor = new Doctor
                {
                    Id = Guid.NewGuid(),
                    UserId = createUserResult.UserId,
                    FacilityDepartmentId = request.FacilityDepartmentId.GetValueOrDefault(),
                    FullName = fullName,
                    Specialty = NormalizeText(request.Qualification),
                    DepartmentRole = request.DepartmentRole.GetValueOrDefault(),
                    YearsOfExperience = request.YearsOfExperience,
                    IsActive = true,
                    CreatedAt = utcNow,
                };

                doctorId = doctor.Id;
                _unitOfWork.Doctors.Add(doctor);
            }

            invitation.Status = DoctorInvitationStatus.Used;
            invitation.UsedAt = utcNow;
            invitation.UpdatedAt = utcNow;

            _unitOfWork.DoctorInvitations.Update(invitation);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new RegisterDoctorByInvitationResponse
            {
                UserId = createUserResult.UserId,
                DoctorId = doctorId,
                Email = invitation.Email,
                FullName = fullName!,
                Role = DoctorRoleName,
                Message = "Doctor account registered successfully.",
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<DoctorInvitationResponse?> RevokeInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        if (invitationId == Guid.Empty)
        {
            throw new ArgumentException("Invalid invitation id.");
        }

        var invitation = await _unitOfWork.DoctorInvitations.GetByIdAsync(
            invitationId,
            cancellationToken);

        if (invitation is null || invitation.IsDeleted)
        {
            return null;
        }

        if (invitation.Status == DoctorInvitationStatus.Used || invitation.UsedAt is not null)
        {
            throw new InvalidOperationException("Used invitation cannot be revoked.");
        }

        var utcNow = DateTime.UtcNow;
        invitation.Status = DoctorInvitationStatus.Revoked;
        invitation.RevokedAt = utcNow;
        invitation.UpdatedAt = utcNow;

        _unitOfWork.DoctorInvitations.Update(invitation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(invitation);
    }

    private async Task<(DoctorInvitation? Invitation, ValidateDoctorInvitationResponse Response)> ValidateInvitationInternalAsync(
        string token,
        bool markExpired,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, Invalid("Invitation token is required."));
        }

        var tokenHash = _tokenService.HashToken(token);
        var invitation = await _unitOfWork.DoctorInvitations.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (invitation is null)
        {
            return (null, Invalid("Invalid invitation link."));
        }

        if (invitation.Status == DoctorInvitationStatus.Used || invitation.UsedAt is not null)
        {
            return (invitation, Invalid("Invitation link has already been used."));
        }

        if (invitation.Status == DoctorInvitationStatus.Revoked || invitation.RevokedAt is not null)
        {
            return (invitation, Invalid("Invitation link has been revoked."));
        }

        if (invitation.Status == DoctorInvitationStatus.Expired
            || invitation.ExpiresAt <= DateTime.UtcNow)
        {
            if (markExpired && invitation.Status != DoctorInvitationStatus.Expired)
            {
                var utcNow = DateTime.UtcNow;
                invitation.Status = DoctorInvitationStatus.Expired;
                invitation.UpdatedAt = utcNow;
                _unitOfWork.DoctorInvitations.Update(invitation);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return (invitation, Invalid("Invitation link has expired.", invitation.ExpiresAt));
        }

        if (invitation.Status != DoctorInvitationStatus.Pending)
        {
            return (invitation, Invalid("Invitation link is no longer available."));
        }

        Doctor? linkedDoctor = null;
        if (invitation.DoctorId.HasValue)
        {
            linkedDoctor = invitation.Doctor
                ?? await _unitOfWork.Doctors.GetByIdAsync(invitation.DoctorId.Value, cancellationToken);

            if (linkedDoctor is null || linkedDoctor.IsDeleted)
            {
                return (invitation, Invalid("Doctor profile is no longer available.", invitation.ExpiresAt));
            }

            if (linkedDoctor.UserId.HasValue)
            {
                return (invitation, Invalid(
                    "This doctor profile is already linked to a user account.",
                    invitation.ExpiresAt));
            }
        }

        return (invitation, new ValidateDoctorInvitationResponse
        {
            IsValid = true,
            Email = invitation.Email,
            ExpiresAt = invitation.ExpiresAt,
            DoctorId = invitation.DoctorId,
            IsLinkedToExistingDoctorProfile = invitation.DoctorId.HasValue,
            DoctorName = linkedDoctor?.FullName,
            SuggestedFullName = linkedDoctor?.FullName,
            Message = "Invitation link is valid.",
        });
    }

    private async Task<bool> IsValidFacilityDepartmentAsync(
        Guid facilityDepartmentId,
        CancellationToken cancellationToken)
    {
        if (facilityDepartmentId == Guid.Empty)
        {
            return false;
        }

        var facilityDepartment = await _unitOfWork.FacilityDepartments.FirstOrDefaultAsync(
            x =>
                x.Id == facilityDepartmentId
                && !x.IsDeleted
                && !x.Facility.IsDeleted
                && x.Facility.IsActive
                && !x.Department.IsDeleted,
            asNoTracking: true,
            cancellationToken: cancellationToken);

        return facilityDepartment is not null;
    }

    private string BuildRegisterUrl(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(_frontendOptions.BaseUrl))
        {
            throw new InvalidOperationException("Frontend:BaseUrl is required.");
        }

        return $"{_frontendOptions.BaseUrl.TrimEnd('/')}/register-doctor?token={Uri.EscapeDataString(rawToken)}";
    }

    private static string BuildInvitationEmailHtml(string registerUrl)
    {
        var encodedRegisterUrl = HtmlEncoder.Default.Encode(registerUrl);

        return
            $"""
            <html>
            <body style="font-family: Arial, sans-serif;">
              <h2>Lời mời đăng ký tài khoản bác sĩ</h2>
              <p>Xin chào,</p>
              <p>Bạn đã được mời đăng ký tài khoản bác sĩ trên hệ thống MedicalMateAI.</p>
              <p>Vui lòng nhấn vào nút bên dưới để hoàn tất đăng ký:</p>
              <p>
                <a href="{encodedRegisterUrl}" style="display:inline-block;padding:10px 16px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;">
                  Đăng ký tài khoản bác sĩ
                </a>
              </p>
              <p>Liên kết này chỉ có hiệu lực trong <strong>10 phút</strong>.</p>
              <p>Nếu nút không hoạt động, hãy copy link này: {encodedRegisterUrl}</p>
              <p>Nếu bạn không liên quan đến lời mời này, vui lòng bỏ qua email.</p>
              <p>Trân trọng,<br/>MedicalMateAI Team</p>
            </body>
            </html>
            """;
    }

    private Guid? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userIdValue = user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out var userId) && userId != Guid.Empty
            ? userId
            : null;
    }

    private static List<string> ValidateRegisterRequest(RegisterDoctorByInvitationRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            errors.Add("Token is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Password is required.");
        }

        if (request.FacilityDepartmentId.HasValue && request.FacilityDepartmentId.Value == Guid.Empty)
        {
            errors.Add("FacilityDepartmentId is invalid.");
        }

        if (request.DepartmentRole.HasValue && !Enum.IsDefined(typeof(DepartmentRole), request.DepartmentRole.Value))
        {
            errors.Add("DepartmentRole is invalid.");
        }

        if (request.YearsOfExperience.HasValue && request.YearsOfExperience.Value < 0)
        {
            errors.Add("YearsOfExperience must be greater than or equal to 0.");
        }

        return errors;
    }

    private static List<string> ValidateNewDoctorProfileRequest(
        RegisterDoctorByInvitationRequest request,
        string? fullName)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors.Add("FullName is required.");
        }

        if (!request.FacilityDepartmentId.HasValue)
        {
            errors.Add("FacilityDepartmentId is required.");
        }

        if (!request.DepartmentRole.HasValue)
        {
            errors.Add("DepartmentRole is required.");
        }

        return errors;
    }

    private static string NormalizeEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Email is required.");
        }

        var email = value.Trim().ToLowerInvariant();

        try
        {
            var address = new MailAddress(email);
            if (!string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException("Email format is invalid.");
        }

        return email;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static ValidateDoctorInvitationResponse Invalid(
        string message,
        DateTime? expiresAt = null)
    {
        return new ValidateDoctorInvitationResponse
        {
            IsValid = false,
            ExpiresAt = expiresAt,
            Message = message,
        };
    }

    private static DoctorInvitationResponse MapToResponse(
        DoctorInvitation invitation,
        Doctor? doctor = null)
    {
        var linkedDoctor = doctor ?? invitation.Doctor;

        return new DoctorInvitationResponse
        {
            Id = invitation.Id,
            Email = invitation.Email,
            DoctorId = invitation.DoctorId,
            DoctorName = linkedDoctor?.FullName,
            IsLinkedToExistingDoctorProfile = invitation.DoctorId.HasValue,
            ExpiresAt = invitation.ExpiresAt,
            Status = invitation.Status.ToString(),
            CreatedAt = invitation.CreatedAt,
            UsedAt = invitation.UsedAt,
        };
    }
}

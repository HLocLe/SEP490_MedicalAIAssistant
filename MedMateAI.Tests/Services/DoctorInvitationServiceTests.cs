using System.Security.Claims;
using MedMateAI.Application.DTOs.DoctorInvitations.Requests;
using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class DoctorInvitationServiceTests
{
    private const string HashedToken = "hashed-token";

    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IDoctorInvitationRepository> _invitationRepoMock = null!;
    private Mock<IDoctorRepository> _doctorRepoMock = null!;
    private Mock<IFacilityDepartmentRepository> _facilityDepartmentRepoMock = null!;
    private Mock<IEmailSender> _emailSenderMock = null!;
    private Mock<IInvitationTokenService> _tokenServiceMock = null!;
    private Mock<IDoctorAccountRegistrationService> _accountRegistrationServiceMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private DoctorInvitationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _invitationRepoMock = new Mock<IDoctorInvitationRepository>();
        _doctorRepoMock = new Mock<IDoctorRepository>();
        _facilityDepartmentRepoMock = new Mock<IFacilityDepartmentRepository>();
        _emailSenderMock = new Mock<IEmailSender>();
        _tokenServiceMock = new Mock<IInvitationTokenService>();
        _accountRegistrationServiceMock = new Mock<IDoctorAccountRegistrationService>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        _unitOfWorkMock.Setup(u => u.DoctorInvitations).Returns(_invitationRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Doctors).Returns(_doctorRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.FacilityDepartments).Returns(_facilityDepartmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(t => t.GenerateToken()).Returns("raw-token");
        _tokenServiceMock.Setup(t => t.HashToken(It.IsAny<string>())).Returns(HashedToken);

        _accountRegistrationServiceMock
            .Setup(s => s.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _accountRegistrationServiceMock
            .Setup(s => s.CreateDoctorUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, Guid.NewGuid(), Enumerable.Empty<string>()));

        _invitationRepoMock
            .Setup(r => r.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DoctorInvitation?)null);
        _invitationRepoMock
            .Setup(r => r.GetPendingByDoctorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DoctorInvitation?)null);

        _facilityDepartmentRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<FacilityDepartment, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacilityDepartment { Id = Guid.NewGuid() });

        SetAuthenticatedUser(null);

        var frontendOptions = Options.Create(new FrontendOptions { BaseUrl = "http://localhost:3000" });

        _service = new DoctorInvitationService(
            _unitOfWorkMock.Object,
            _emailSenderMock.Object,
            _tokenServiceMock.Object,
            _accountRegistrationServiceMock.Object,
            _httpContextAccessorMock.Object,
            frontendOptions);
    }

    private void SetAuthenticatedUser(Guid? userId)
    {
        if (userId is null)
        {
            _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
            return;
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);
    }

    private static DoctorInvitation MakeInvitation(
        Guid? id = null,
        string email = "doctor@example.com",
        Guid? doctorId = null,
        DoctorInvitationStatus status = DoctorInvitationStatus.Pending,
        DateTime? expiresAt = null,
        DateTime? usedAt = null,
        DateTime? revokedAt = null,
        Doctor? doctor = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            Email = email,
            TokenHash = HashedToken,
            DoctorId = doctorId,
            Status = status,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(2),
            UsedAt = usedAt,
            RevokedAt = revokedAt,
            Doctor = doctor,
        };

    private static Doctor MakeDoctor(
        Guid? id = null,
        Guid? userId = null,
        bool isDeleted = false,
        string? fullName = "Dr. A") => new()
        {
            Id = id ?? Guid.NewGuid(),
            FacilityDepartmentId = Guid.NewGuid(),
            FullName = fullName,
            UserId = userId,
            IsDeleted = isDeleted,
            DepartmentRole = DepartmentRole.Doctor,
        };

    // ── CreateInvitationAsync ────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public void CreateInvitationAsync_NullRequest_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(async () => await _service.CreateInvitationAsync(null!));
    }

    [Test]
    [Category("A")]
    public void CreateInvitationAsync_InvalidEmailFormat_ThrowsArgumentException()
    {
        var request = new CreateDoctorInvitationRequest { Email = "not-an-email" };

        var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _service.CreateInvitationAsync(request));
        Assert.That(ex!.Message, Is.EqualTo("Email format is invalid."));
    }

    [Test]
    [Category("A")]
    public void CreateInvitationAsync_EmailAlreadyRegistered_ThrowsInvalidOperationException()
    {
        _accountRegistrationServiceMock
            .Setup(s => s.EmailExistsAsync("doctor@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateDoctorInvitationRequest { Email = "doctor@example.com" };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.CreateInvitationAsync(request));
    }

    [Test]
    [Category("B")]
    public void CreateInvitationAsync_DoctorIdEmptyGuid_ThrowsArgumentException()
    {
        var request = new CreateDoctorInvitationRequest { Email = "doctor@example.com", DoctorId = Guid.Empty };

        Assert.ThrowsAsync<ArgumentException>(async () => await _service.CreateInvitationAsync(request));
    }

    [Test]
    [Category("A")]
    public void CreateInvitationAsync_DoctorNotFound_ThrowsKeyNotFoundException()
    {
        var doctorId = Guid.NewGuid();
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var request = new CreateDoctorInvitationRequest { Email = "doctor@example.com", DoctorId = doctorId };

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await _service.CreateInvitationAsync(request));
    }

    [Test]
    [Category("A")]
    public void CreateInvitationAsync_DoctorAlreadyLinked_ThrowsInvalidOperationException()
    {
        var doctorId = Guid.NewGuid();
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor(doctorId, userId: Guid.NewGuid()));

        var request = new CreateDoctorInvitationRequest { Email = "doctor@example.com", DoctorId = doctorId };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.CreateInvitationAsync(request));
    }

    [Test]
    [Category("A")]
    public void CreateInvitationAsync_ActiveInvitationExistsForDoctor_ThrowsInvalidOperationException()
    {
        var doctorId = Guid.NewGuid();
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor(doctorId));
        _invitationRepoMock.Setup(r => r.GetPendingByDoctorIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(doctorId: doctorId));

        var request = new CreateDoctorInvitationRequest { Email = "doctor@example.com", DoctorId = doctorId };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.CreateInvitationAsync(request));
    }

    [Test]
    [Category("A")]
    public void CreateInvitationAsync_ActiveInvitationExistsForEmail_ThrowsInvalidOperationException()
    {
        _invitationRepoMock.Setup(r => r.GetPendingByEmailAsync("doctor@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation());

        var request = new CreateDoctorInvitationRequest { Email = "doctor@example.com" };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.CreateInvitationAsync(request));
    }

    [Test]
    [Category("N")]
    public async Task CreateInvitationAsync_ValidRequest_CreatesInvitationAndSendsEmail()
    {
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId);

        DoctorInvitation? captured = null;
        _invitationRepoMock.Setup(r => r.Add(It.IsAny<DoctorInvitation>()))
            .Callback<DoctorInvitation>(i => captured = i);

        var request = new CreateDoctorInvitationRequest { Email = "  Doctor@Example.com  " };

        var result = await _service.CreateInvitationAsync(request);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Email, Is.EqualTo("doctor@example.com"));
        Assert.That(result.Status, Is.EqualTo(DoctorInvitationStatus.Pending.ToString()));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.CreatedByAdminId, Is.EqualTo(adminId));
        _emailSenderMock.Verify(s => s.SendAsync(
            "doctor@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public void CreateInvitationAsync_EmailSendFails_MarksRevokedAndThrows()
    {
        DoctorInvitation? captured = null;
        _invitationRepoMock.Setup(r => r.Add(It.IsAny<DoctorInvitation>()))
            .Callback<DoctorInvitation>(i => captured = i);

        _emailSenderMock
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("smtp down"));

        var request = new CreateDoctorInvitationRequest { Email = "doctor@example.com" };

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.CreateInvitationAsync(request));

        Assert.That(ex!.Message, Is.EqualTo("Failed to send invitation email."));
        Assert.That(captured!.Status, Is.EqualTo(DoctorInvitationStatus.Revoked));
        _invitationRepoMock.Verify(r => r.Update(captured), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── ValidateInvitationAsync ──────────────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task ValidateInvitationAsync_BlankToken_ReturnsInvalid()
    {
        var result = await _service.ValidateInvitationAsync(" ");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Message, Is.EqualTo("Invitation token is required."));
    }

    [Test]
    [Category("A")]
    public async Task ValidateInvitationAsync_TokenNotFound_ReturnsInvalid()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DoctorInvitation?)null);

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Message, Is.EqualTo("Invalid invitation link."));
    }

    [Test]
    [Category("A")]
    public async Task ValidateInvitationAsync_AlreadyUsed_ReturnsInvalid()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(status: DoctorInvitationStatus.Used, usedAt: DateTime.UtcNow));

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Message, Is.EqualTo("Invitation link has already been used."));
    }

    [Test]
    [Category("A")]
    public async Task ValidateInvitationAsync_Revoked_ReturnsInvalid()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(status: DoctorInvitationStatus.Revoked, revokedAt: DateTime.UtcNow));

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Message, Is.EqualTo("Invitation link has been revoked."));
    }

    [Test]
    [Category("A")]
    public async Task ValidateInvitationAsync_ExpiredByDate_MarksStatusExpiredAndReturnsInvalid()
    {
        var invitation = MakeInvitation(expiresAt: DateTime.UtcNow.AddMinutes(-1));
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Message, Is.EqualTo("Invitation link has expired."));
        Assert.That(invitation.Status, Is.EqualTo(DoctorInvitationStatus.Expired));
        _invitationRepoMock.Verify(r => r.Update(invitation), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task ValidateInvitationAsync_AlreadyMarkedExpired_DoesNotSaveAgain()
    {
        var invitation = MakeInvitation(status: DoctorInvitationStatus.Expired, expiresAt: DateTime.UtcNow.AddMinutes(-5));
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.False);
        _invitationRepoMock.Verify(r => r.Update(It.IsAny<DoctorInvitation>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task ValidateInvitationAsync_DoctorProfileGone_ReturnsInvalid()
    {
        var doctorId = Guid.NewGuid();
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(doctorId: doctorId));
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Message, Is.EqualTo("Doctor profile is no longer available."));
    }

    [Test]
    [Category("A")]
    public async Task ValidateInvitationAsync_DoctorAlreadyLinked_ReturnsInvalid()
    {
        var doctorId = Guid.NewGuid();
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(doctorId: doctorId));
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor(doctorId, userId: Guid.NewGuid()));

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Message, Is.EqualTo("This doctor profile is already linked to a user account."));
    }

    [Test]
    [Category("N")]
    public async Task ValidateInvitationAsync_ValidWithoutLinkedDoctor_ReturnsValidResponse()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation());

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsLinkedToExistingDoctorProfile, Is.False);
    }

    [Test]
    [Category("N")]
    public async Task ValidateInvitationAsync_ValidWithLinkedDoctor_ReturnsDoctorName()
    {
        var doctor = MakeDoctor(fullName: "Dr. Linked");
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(doctorId: doctor.Id, doctor: doctor));

        var result = await _service.ValidateInvitationAsync("raw-token");

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsLinkedToExistingDoctorProfile, Is.True);
        Assert.That(result.DoctorName, Is.EqualTo("Dr. Linked"));
    }

    // ── RegisterDoctorAsync ──────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_NullRequest_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(async () => await _service.RegisterDoctorAsync(null!));
    }

    [Test]
    [Category("B")]
    public void RegisterDoctorAsync_MissingTokenAndPassword_ThrowsArgumentException()
    {
        var request = new RegisterDoctorByInvitationRequest { Token = " ", Password = " " };

        var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _service.RegisterDoctorAsync(request));
        Assert.That(ex!.Message, Does.Contain("Token is required."));
        Assert.That(ex.Message, Does.Contain("Password is required."));
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_InvalidInvitation_ThrowsInvalidOperationException()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DoctorInvitation?)null);

        var request = new RegisterDoctorByInvitationRequest { Token = "raw-token", Password = "P@ssw0rd" };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.RegisterDoctorAsync(request));
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_EmailAlreadyRegistered_ThrowsInvalidOperationException()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation());
        _accountRegistrationServiceMock
            .Setup(s => s.EmailExistsAsync("doctor@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RegisterDoctorByInvitationRequest
        {
            Token = "raw-token",
            Password = "P@ssw0rd",
            FullName = "New Doctor",
            FacilityDepartmentId = Guid.NewGuid(),
            DepartmentRole = DepartmentRole.Doctor,
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.RegisterDoctorAsync(request));
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_NewProfileMissingFacilityDepartmentAndRole_ThrowsArgumentException()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation());

        var request = new RegisterDoctorByInvitationRequest
        {
            Token = "raw-token",
            Password = "P@ssw0rd",
            FullName = "New Doctor",
        };

        var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _service.RegisterDoctorAsync(request));
        Assert.That(ex!.Message, Does.Contain("FacilityDepartmentId is required."));
        Assert.That(ex.Message, Does.Contain("DepartmentRole is required."));
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_NewProfileInvalidFacilityDepartment_ThrowsArgumentException()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation());
        _facilityDepartmentRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<FacilityDepartment, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityDepartment?)null);

        var request = new RegisterDoctorByInvitationRequest
        {
            Token = "raw-token",
            Password = "P@ssw0rd",
            FullName = "New Doctor",
            FacilityDepartmentId = Guid.NewGuid(),
            DepartmentRole = DepartmentRole.Doctor,
        };

        Assert.ThrowsAsync<ArgumentException>(async () => await _service.RegisterDoctorAsync(request));
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_ExistingProfileGone_ThrowsInvalidOperationException()
    {
        var doctorId = Guid.NewGuid();
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(doctorId: doctorId));
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var request = new RegisterDoctorByInvitationRequest { Token = "raw-token", Password = "P@ssw0rd" };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.RegisterDoctorAsync(request));
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_ExistingProfileAlreadyLinked_ThrowsInvalidOperationException()
    {
        var doctor = MakeDoctor(userId: Guid.NewGuid());
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(doctorId: doctor.Id));
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var request = new RegisterDoctorByInvitationRequest { Token = "raw-token", Password = "P@ssw0rd" };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.RegisterDoctorAsync(request));
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_ExistingProfileNoFullNameAvailable_ThrowsArgumentException()
    {
        var doctor = MakeDoctor(fullName: null);
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(doctorId: doctor.Id));
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var request = new RegisterDoctorByInvitationRequest { Token = "raw-token", Password = "P@ssw0rd" };

        Assert.ThrowsAsync<ArgumentException>(async () => await _service.RegisterDoctorAsync(request));
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_CreateUserFails_RollsBackAndThrows()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation());
        _accountRegistrationServiceMock
            .Setup(s => s.CreateDoctorUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, Guid.Empty, new[] { "Password too weak." }));

        var request = new RegisterDoctorByInvitationRequest
        {
            Token = "raw-token",
            Password = "weak",
            FullName = "New Doctor",
            FacilityDepartmentId = Guid.NewGuid(),
            DepartmentRole = DepartmentRole.Doctor,
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.RegisterDoctorAsync(request));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public void RegisterDoctorAsync_SaveChangesThrows_RollsBackAndRethrows()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation());
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));

        var request = new RegisterDoctorByInvitationRequest
        {
            Token = "raw-token",
            Password = "P@ssw0rd",
            FullName = "New Doctor",
            FacilityDepartmentId = Guid.NewGuid(),
            DepartmentRole = DepartmentRole.Doctor,
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.RegisterDoctorAsync(request));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task RegisterDoctorAsync_NewProfile_Valid_CommitsAndReturnsResponse()
    {
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation());

        var request = new RegisterDoctorByInvitationRequest
        {
            Token = "raw-token",
            Password = "P@ssw0rd",
            FullName = "New Doctor",
            FacilityDepartmentId = Guid.NewGuid(),
            DepartmentRole = DepartmentRole.Doctor,
        };

        var result = await _service.RegisterDoctorAsync(request);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.FullName, Is.EqualTo("New Doctor"));
        Assert.That(result.Email, Is.EqualTo("doctor@example.com"));
        _doctorRepoMock.Verify(r => r.Add(It.IsAny<Doctor>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task RegisterDoctorAsync_ExistingProfile_Valid_UpdatesDoctorAndCommitsResponse()
    {
        var doctor = MakeDoctor(fullName: null);
        _invitationRepoMock.Setup(r => r.GetByTokenHashAsync(HashedToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(doctorId: doctor.Id));
        _doctorRepoMock.Setup(r => r.GetByIdAsync(doctor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var request = new RegisterDoctorByInvitationRequest
        {
            Token = "raw-token",
            Password = "P@ssw0rd",
            FullName = "Existing Doctor",
        };

        var result = await _service.RegisterDoctorAsync(request);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.DoctorId, Is.EqualTo(doctor.Id));
        Assert.That(doctor.FullName, Is.EqualTo("Existing Doctor"));
        _doctorRepoMock.Verify(r => r.Update(doctor), Times.Once);
        _doctorRepoMock.Verify(r => r.Add(It.IsAny<Doctor>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── RevokeInvitationAsync ────────────────────────────────────────────────

    [Test]
    [Category("B")]
    public void RevokeInvitationAsync_EmptyId_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(async () => await _service.RevokeInvitationAsync(Guid.Empty));
    }

    [Test]
    [Category("A")]
    public async Task RevokeInvitationAsync_NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _invitationRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DoctorInvitation?)null);

        Assert.That(await _service.RevokeInvitationAsync(id), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task RevokeInvitationAsync_Deleted_ReturnsNull()
    {
        var id = Guid.NewGuid();
        var invitation = MakeInvitation(id);
        invitation.IsDeleted = true;
        _invitationRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        Assert.That(await _service.RevokeInvitationAsync(id), Is.Null);
    }

    [Test]
    [Category("A")]
    public void RevokeInvitationAsync_AlreadyUsed_ThrowsInvalidOperationException()
    {
        var id = Guid.NewGuid();
        _invitationRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInvitation(id, status: DoctorInvitationStatus.Used, usedAt: DateTime.UtcNow));

        Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.RevokeInvitationAsync(id));
    }

    [Test]
    [Category("N")]
    public async Task RevokeInvitationAsync_ValidId_RevokesAndReturnsResponse()
    {
        var id = Guid.NewGuid();
        var invitation = MakeInvitation(id);
        _invitationRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var result = await _service.RevokeInvitationAsync(id);

        Assert.That(result, Is.Not.Null);
        Assert.That(invitation.Status, Is.EqualTo(DoctorInvitationStatus.Revoked));
        Assert.That(invitation.RevokedAt, Is.Not.Null);
        _invitationRepoMock.Verify(r => r.Update(invitation), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

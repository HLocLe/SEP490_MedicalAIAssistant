using System.Linq.Expressions;
using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.PatientProfiles.Requests;
using MedMateAI.Application.DTOs.PatientProfiles.Responses;
using MedMateAI.Application.DTOs.Users.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class PatientProfileServiceTests
{
    private Mock<IUserService> _userServiceMock = null!;
    private Mock<IGenericRepository<PatientProfile>> _patientProfilesMock = null!;
    private Mock<IGenericRepository<PatientChronicDisease>> _chronicDiseasesMock = null!;
    private Mock<IMapper> _mapperMock = null!;
    private Mock<IUnitOfWork> _uowMock = null!;
    private PatientProfileService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _userServiceMock = new Mock<IUserService>();
        _patientProfilesMock = new Mock<IGenericRepository<PatientProfile>>();
        _chronicDiseasesMock = new Mock<IGenericRepository<PatientChronicDisease>>();
        _mapperMock = new Mock<IMapper>();
        _uowMock = new Mock<IUnitOfWork>();

        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _userServiceMock.Setup(service => service.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationUserResponse
            {
                Id = _userId,
                DateOfBirth = new DateOnly(2000, 1, 1),
            });
        _userServiceMock.Setup(service => service.IsInRoleAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userServiceMock.Setup(service => service.GetUserByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, CancellationToken _) => new ApplicationUserResponse { Id = userId });
        _userServiceMock.Setup(service => service.MarkPatientProfileCompletedAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, Array.Empty<string>()));

        _service = new PatientProfileService(
            _userServiceMock.Object,
            _patientProfilesMock.Object,
            _chronicDiseasesMock.Object,
            _mapperMock.Object,
            _uowMock.Object
        );

        // Standard AutoMapper mock mapping setup
        _mapperMock.Setup(m => m.Map<PatientProfileResponse>(It.IsAny<PatientProfile>()))
            .Returns((PatientProfile src) => new PatientProfileResponse
            {
                Id = src.Id,
                UserId = src.UserId,
                BloodType = src.BloodType,
                Height = src.Height,
                Weight = src.Weight,
                AllergyNote = src.AllergyNote,
                IsDeleted = src.IsDeleted,
                CreatedAt = src.CreatedAt,
                UpdatedAt = src.UpdatedAt,
                ChronicDiseases = src.ChronicDiseases.Select(d => new PatientChronicDiseaseResponse
                {
                    Id = d.Id,
                    DiseaseName = d.DiseaseName,
                    From = d.From,
                    To = d.To,
                    Note = d.Note
                }).ToList()
            });
    }

    // â”€â”€ DeleteMyProfileAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task DeleteMyProfileAsync_Unauthenticated_ReturnsUnauthorized()
    {
        _userServiceMock.Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUserResponse?)null);

        var (succeeded, errors) = await _service.DeleteMyProfileAsync();

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Người dùng chưa đăng nhập."));
    }

    [Test]
    [Category("A")]
    public async Task DeleteMyProfileAsync_ProfileNotFound_ReturnsNotFound()
    {
        var currentUser = new ApplicationUserResponse { Id = Guid.NewGuid() };
        _userServiceMock.Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentUser);

        _patientProfilesMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PatientProfile, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        var (succeeded, errors) = await _service.DeleteMyProfileAsync();

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Không tìm thấy hồ sơ bệnh nhân."));
    }

    [Test]
    [Category("N")]
    public async Task DeleteMyProfileAsync_Success_SoftDeletesProfileAndChronicDiseases()
    {
        var currentUser = new ApplicationUserResponse { Id = Guid.NewGuid() };
        _userServiceMock.Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentUser);

        var profile = new PatientProfile { Id = Guid.NewGuid(), UserId = currentUser.Id, IsDeleted = false };
        _patientProfilesMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PatientProfile, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var diseases = new List<PatientChronicDisease>
        {
            new() { Id = Guid.NewGuid(), PatientProfileId = profile.Id, IsDeleted = false }
        };
        var pagedDiseases = new PagedResult<PatientChronicDisease>
        {
            Items = diseases,
            PageNumber = 1,
            PageSize = 1000,
            TotalCount = 1,
            TotalPages = 1
        };
        _chronicDiseasesMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<PatientChronicDisease, bool>>>(),
                It.IsAny<Func<IQueryable<PatientChronicDisease>, IOrderedQueryable<PatientChronicDisease>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedDiseases);

        var (succeeded, errors) = await _service.DeleteMyProfileAsync();

        Assert.That(succeeded, Is.True);
        Assert.That(profile.IsDeleted, Is.True);
        Assert.That(diseases[0].IsDeleted, Is.True);
        _patientProfilesMock.Verify(r => r.Update(profile), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ ListPatientProfilesAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListPatientProfilesAsync_ValidPagedRequest_ReturnsPagedResponseWithChronicDiseases()
    {
        var profiles = new List<PatientProfile>
        {
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Height = 170 }
        };
        var pagedProfiles = new PagedResult<PatientProfile>
        {
            Items = profiles,
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1
        };
        _patientProfilesMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<PatientProfile, bool>>>(),
                It.IsAny<Func<IQueryable<PatientProfile>, IOrderedQueryable<PatientProfile>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedProfiles);

        var diseases = new List<PatientChronicDisease>
        {
            new() { Id = Guid.NewGuid(), PatientProfileId = profiles[0].Id, DiseaseName = "Asthma" }
        };
        var pagedDiseases = new PagedResult<PatientChronicDisease>
        {
            Items = diseases,
            PageNumber = 1,
            PageSize = 1000,
            TotalCount = 1,
            TotalPages = 1
        };
        _chronicDiseasesMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<PatientChronicDisease, bool>>>(),
                It.IsAny<Func<IQueryable<PatientChronicDisease>, IOrderedQueryable<PatientChronicDisease>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedDiseases);

        var result = await _service.ListPatientProfilesAsync(1, 10);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].ChronicDiseases, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].ChronicDiseases[0].DiseaseName, Is.EqualTo("Asthma"));
    }

    // â”€â”€ GetPatientProfileByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task GetPatientProfileByIdAsync_Found_ReturnsProfile()
    {
        var id = Guid.NewGuid();
        var profile = new PatientProfile { Id = id, UserId = _userId, IsDeleted = false };
        _patientProfilesMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _chronicDiseasesMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<PatientChronicDisease, bool>>>(),
                It.IsAny<Func<IQueryable<PatientChronicDisease>, IOrderedQueryable<PatientChronicDisease>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientChronicDisease>
            {
                Items = new List<PatientChronicDisease>(),
                PageNumber = 1,
                PageSize = 1000,
                TotalCount = 0,
                TotalPages = 0
            });

        var (notFound, data) = await _service.GetPatientProfileByIdAsync(id);

        Assert.That(notFound, Is.False);
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.Id, Is.EqualTo(id));
    }

    [Test]
    [Category("A")]
    public async Task GetPatientProfileByIdAsync_NotFoundOrDeleted_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _patientProfilesMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        var (notFound, data) = await _service.GetPatientProfileByIdAsync(id);

        Assert.That(notFound, Is.True);
        Assert.That(data, Is.Null);
    }

    // â”€â”€ GetPatientProfileByUserIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetPatientProfileByUserIdAsync_EmptyUserId_ReturnsNotFound()
    {
        var (notFound, data) = await _service.GetPatientProfileByUserIdAsync(Guid.Empty);

        Assert.That(notFound, Is.True);
        Assert.That(data, Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetPatientProfileByUserIdAsync_Success_ReturnsProfile()
    {
        var userId = Guid.NewGuid();
        var profile = new PatientProfile { Id = Guid.NewGuid(), UserId = userId, IsDeleted = false };
        _userServiceMock.Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationUserResponse { Id = userId });
        _patientProfilesMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PatientProfile, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        _chronicDiseasesMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<PatientChronicDisease, bool>>>(),
                It.IsAny<Func<IQueryable<PatientChronicDisease>, IOrderedQueryable<PatientChronicDisease>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientChronicDisease>
            {
                Items = new List<PatientChronicDisease>(),
                PageNumber = 1,
                PageSize = 1000,
                TotalCount = 0,
                TotalPages = 0
            });

        var user = new ApplicationUserResponse { Id = userId, IsProfileCompleted = true };
        _userServiceMock.Setup(s => s.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var (notFound, data) = await _service.GetPatientProfileByUserIdAsync(userId);

        Assert.That(notFound, Is.False);
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.IsProfileCompleted, Is.True);
    }

    // â”€â”€ CreatePatientProfileAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task CreatePatientProfileAsync_Unauthenticated_ReturnsFailed()
    {
        _userServiceMock.Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUserResponse?)null);
        var req = new CreatePatientProfileRequest { UserId = Guid.Empty };
        var (succeeded, errors, data) = await _service.CreatePatientProfileAsync(req);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Người dùng chưa đăng nhập."));
    }

    [Test]
    [Category("A")]
    public async Task CreatePatientProfileAsync_DuplicateProfile_ReturnsFailed()
    {
        var userId = _userId;
        var req = new CreatePatientProfileRequest { UserId = userId };
        
        var existing = new PatientProfile { Id = Guid.NewGuid(), UserId = userId };
        _patientProfilesMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PatientProfile, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var (succeeded, errors, data) = await _service.CreatePatientProfileAsync(req);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Người dùng này đã có hồ sơ bệnh nhân."));
    }

    [Test]
    [Category("A")]
    public async Task CreatePatientProfileAsync_ChronicDiseaseValidationError_ReturnsFailed()
    {
        var req = new CreatePatientProfileRequest
        {
            UserId = _userId,
            ChronicDiseases = new List<PatientChronicDiseaseItemCreateRequest>
            {
                new() { DiseaseName = "  ", From = new DateOnly(2026, 1, 10), To = new DateOnly(2026, 1, 1) } // invalid name & dates
            }
        };

        _patientProfilesMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PatientProfile, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        var (succeeded, errors, data) = await _service.CreatePatientProfileAsync(req);

        Assert.That(succeeded, Is.False);
        Assert.That(errors.Count(), Is.EqualTo(2));
    }

    [Test]
    [Category("N")]
    public async Task CreatePatientProfileAsync_Success_CreatesProfileAndChronicDiseases()
    {
        var userId = _userId;
        var req = new CreatePatientProfileRequest
        {
            UserId = userId,
            BloodType = "O+",
            Height = 175,
            Weight = 70,
            ChronicDiseases = new List<PatientChronicDiseaseItemCreateRequest>
            {
                new() { DiseaseName = "Asthma", From = new DateOnly(2020, 1, 1) }
            }
        };

        _patientProfilesMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PatientProfile, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        _userServiceMock.Setup(s => s.MarkPatientProfileCompletedAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, Array.Empty<string>()));

        _chronicDiseasesMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<PatientChronicDisease, bool>>>(),
                It.IsAny<Func<IQueryable<PatientChronicDisease>, IOrderedQueryable<PatientChronicDisease>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientChronicDisease>
            {
                Items = new List<PatientChronicDisease>(),
                PageNumber = 1,
                PageSize = 1000,
                TotalCount = 0,
                TotalPages = 0
            });

        var (succeeded, errors, data) = await _service.CreatePatientProfileAsync(req);

        Assert.That(succeeded, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.BloodType, Is.EqualTo("O+"));
        _patientProfilesMock.Verify(r => r.Add(It.IsAny<PatientProfile>()), Times.Once);
        _chronicDiseasesMock.Verify(r => r.Add(It.IsAny<PatientChronicDisease>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // â”€â”€ UpdatePatientProfileAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdatePatientProfileAsync_EmptyId_ReturnsFailed()
    {
        var req = new UpdatePatientProfileRequest();
        var (succeeded, notFound, errors, data) = await _service.UpdatePatientProfileAsync(Guid.Empty, req);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Id hồ sơ bệnh nhân không hợp lệ."));
    }

    [Test]
    [Category("A")]
    public async Task UpdatePatientProfileAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _patientProfilesMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        var req = new UpdatePatientProfileRequest();
        var (succeeded, notFound, errors, data) = await _service.UpdatePatientProfileAsync(id, req);

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task UpdatePatientProfileAsync_Success_UpdatesFieldsAndSyncsChronicDiseases()
    {
        var id = Guid.NewGuid();
        var profile = new PatientProfile { Id = id, UserId = _userId, BloodType = "A-", Height = 160 };
        _patientProfilesMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var existingDisease = new PatientChronicDisease { Id = Guid.NewGuid(), PatientProfileId = id, DiseaseName = "Diabetes" };
        var pagedDiseases = new PagedResult<PatientChronicDisease>
        {
            Items = new List<PatientChronicDisease> { existingDisease },
            PageNumber = 1,
            PageSize = 1000,
            TotalCount = 1,
            TotalPages = 1
        };
        _chronicDiseasesMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<PatientChronicDisease, bool>>>(),
                It.IsAny<Func<IQueryable<PatientChronicDisease>, IOrderedQueryable<PatientChronicDisease>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedDiseases);

        var req = new UpdatePatientProfileRequest
        {
            BloodType = "B+",
            Height = 165,
            ChronicDiseases = new List<PatientChronicDiseaseItemUpdateRequest>
            {
                new() { Id = existingDisease.Id, DiseaseName = "Diabetes Type 2" }, // Update existing
                new() { DiseaseName = "Hypertension" } // Add new
            }
        };

        var (succeeded, notFound, errors, data) = await _service.UpdatePatientProfileAsync(id, req);

        Assert.That(succeeded, Is.True);
        Assert.That(profile.BloodType, Is.EqualTo("B+"));
        Assert.That(profile.Height, Is.EqualTo(165));
        Assert.That(existingDisease.DiseaseName, Is.EqualTo("Diabetes Type 2"));
        _chronicDiseasesMock.Verify(r => r.Add(It.IsAny<PatientChronicDisease>()), Times.Once); // new one added
        _patientProfilesMock.Verify(r => r.Update(profile), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ SoftDeletePatientProfileAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task SoftDeletePatientProfileAsync_EmptyId_ReturnsFailed()
    {
        var (succeeded, notFound, errors) = await _service.SoftDeletePatientProfileAsync(Guid.Empty);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Id hồ sơ bệnh nhân không hợp lệ."));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeletePatientProfileAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _patientProfilesMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        var (succeeded, notFound, errors) = await _service.SoftDeletePatientProfileAsync(id);

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task SoftDeletePatientProfileAsync_Success_SoftDeletesProfileAndChronicDiseases()
    {
        var id = Guid.NewGuid();
        var profile = new PatientProfile { Id = id, UserId = _userId, IsDeleted = false };
        _patientProfilesMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var diseases = new List<PatientChronicDisease>
        {
            new() { Id = Guid.NewGuid(), PatientProfileId = id, IsDeleted = false }
        };
        var pagedDiseases = new PagedResult<PatientChronicDisease>
        {
            Items = diseases,
            PageNumber = 1,
            PageSize = 1000,
            TotalCount = 1,
            TotalPages = 1
        };
        _chronicDiseasesMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<PatientChronicDisease, bool>>>(),
                It.IsAny<Func<IQueryable<PatientChronicDisease>, IOrderedQueryable<PatientChronicDisease>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedDiseases);

        var (succeeded, notFound, errors) = await _service.SoftDeletePatientProfileAsync(id);

        Assert.That(succeeded, Is.True);
        Assert.That(profile.IsDeleted, Is.True);
        Assert.That(diseases[0].IsDeleted, Is.True);
        _patientProfilesMock.Verify(r => r.Update(profile), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

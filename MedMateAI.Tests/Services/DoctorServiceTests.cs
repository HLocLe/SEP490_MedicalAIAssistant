using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Doctors.Requests;
using MedMateAI.Application.DTOs.Doctors.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class DoctorServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IDoctorRepository> _doctorRepoMock = null!;
    private Mock<IFacilityDepartmentRepository> _facilityDeptRepoMock = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<IRecoveryPlanRealtimeNotifier> _realtimeNotifierMock = null!;
    private DoctorService _service = null!;
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _facilityDeptId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _doctorRepoMock = new Mock<IDoctorRepository>();
        _facilityDeptRepoMock = new Mock<IFacilityDepartmentRepository>();
        _cacheMock = new Mock<IDistributedCache>();
        _realtimeNotifierMock = new Mock<IRecoveryPlanRealtimeNotifier>();

        _unitOfWorkMock.Setup(u => u.Doctors).Returns(_doctorRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.FacilityDepartments).Returns(_facilityDeptRepoMock.Object);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new DoctorService(
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            _realtimeNotifierMock.Object);
    }

    // â”€â”€ ListDoctorsAsync & ListActiveDoctorsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListDoctorsAsync_ReturnsPagedResponse()
    {
        // Arrange
        var pagedResult = new PagedResult<Doctor>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1,
            Items = new List<Doctor>
            {
                new() { Id = _doctorId, FullName = "Dr. Strange", DepartmentRole = DepartmentRole.Doctor }
            }
        };

        _doctorRepoMock.Setup(r => r.GetPagedWithDetailsAsync(1, 10, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _service.ListDoctorsAsync(1, 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].FullName, Is.EqualTo("Dr. Strange"));
    }

    [Test]
    [Category("N")]
    public async Task ListActiveDoctorsAsync_ReturnsPagedResponse()
    {
        // Arrange
        var pagedResult = new PagedResult<Doctor>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1,
            Items = new List<Doctor>
            {
                new() { Id = _doctorId, FullName = "Dr. Strange", DepartmentRole = DepartmentRole.Doctor, IsActive = true }
            }
        };

        _doctorRepoMock.Setup(r => r.GetPagedWithDetailsAsync(1, 10, null, null, null, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _service.ListActiveDoctorsAsync(1, 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Items, Has.Count.EqualTo(1));
    }

    // â”€â”€ GetDoctorByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetDoctorByIdAsync_EmptyId_ReturnsNull()
    {
        Assert.That(await _service.GetDoctorByIdAsync(Guid.Empty), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetDoctorByIdAsync_CacheHit_ReturnsCachedResponse()
    {
        // Arrange
        var response = new DoctorResponse { Id = _doctorId, FullName = "Dr. Strange Cached" };
        var cachedJson = JsonSerializer.Serialize(response);

        _cacheMock.Setup(c => c.GetAsync($"doctors:{_doctorId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _service.GetDoctorByIdAsync(_doctorId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.FullName, Is.EqualTo("Dr. Strange Cached"));
        _doctorRepoMock.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task GetDoctorByIdAsync_CacheMiss_QueriesDbAndSetsCache()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetAsync($"doctors:{_doctorId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var doctor = new Doctor { Id = _doctorId, FullName = "Dr. Strange Db", DepartmentRole = DepartmentRole.Head };
        _doctorRepoMock.Setup(r => r.GetByIdWithDetailsAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        // Act
        var result = await _service.GetDoctorByIdAsync(_doctorId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.FullName, Is.EqualTo("Dr. Strange Db"));
        _cacheMock.Verify(c => c.SetAsync(
            $"doctors:{_doctorId}",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ CreateDoctorAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task CreateDoctorAsync_NullRequest_ReturnsError()
    {
        var result = await _service.CreateDoctorAsync(null!);
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    [Category("A")]
    public async Task CreateDoctorAsync_InvalidInputs_ReturnsErrors()
    {
        var req = new CreateDoctorRequest
        {
            UserId = Guid.Empty,
            FacilityDepartmentId = Guid.Empty,
            FullName = " ",
            YearsOfExperience = -1,
            ImageUrl = "invalid-url",
            DepartmentRole = (DepartmentRole)99
        };

        var result = await _service.CreateDoctorAsync(req);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors.Count(), Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    [Category("A")]
    public async Task CreateDoctorAsync_DuplicateName_ReturnsError()
    {
        // Arrange
        var req = new CreateDoctorRequest
        {
            FacilityDepartmentId = _facilityDeptId,
            FullName = "Dr. Duplicate",
            YearsOfExperience = 5,
            DepartmentRole = DepartmentRole.Doctor,
            IsActive = true
        };

        // Facility department exists
        var facilityDept = new FacilityDepartment
        {
            Id = _facilityDeptId,
            Facility = new MedicalFacility { IsActive = true, IsDeleted = false },
            Department = new MedicalDepartment { IsDeleted = false }
        };
        _facilityDeptRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<FacilityDepartment, bool>>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facilityDept);

        // Duplicate doctor exists
        _doctorRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Doctor, bool>>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Doctor { FullName = "Dr. Duplicate" });

        // Act
        var result = await _service.CreateDoctorAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("Bác sĩ cùng họ tên đã tồn tại trong khoa này"));
    }

    [Test]
    [Category("N")]
    public async Task CreateDoctorAsync_ValidRequest_Success()
    {
        // Arrange
        var req = new CreateDoctorRequest
        {
            UserId = _userId,
            FacilityDepartmentId = _facilityDeptId,
            FullName = "Dr. Strange",
            YearsOfExperience = 5,
            DepartmentRole = DepartmentRole.Doctor,
            IsActive = true
        };

        var facilityDept = new FacilityDepartment
        {
            Id = _facilityDeptId,
            Facility = new MedicalFacility { IsActive = true, IsDeleted = false },
            Department = new MedicalDepartment { IsDeleted = false }
        };
        _facilityDeptRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<FacilityDepartment, bool>>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facilityDept);

        // No duplicate
        _doctorRepoMock.SetupSequence(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Doctor, bool>>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null) // for duplicate name check
            .ReturnsAsync((Doctor?)null); // for link user check

        // Act
        var result = await _service.CreateDoctorAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        _doctorRepoMock.Verify(r => r.Add(It.IsAny<Doctor>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ UpdateDoctorAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task UpdateDoctorAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        _doctorRepoMock.Setup(r => r.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        // Act
        var result = await _service.UpdateDoctorAsync(_doctorId, new UpdateDoctorRequest());

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task UpdateDoctorAsync_ValidRequest_UpdatesAndInvalidatesCache()
    {
        // Arrange
        var existing = new Doctor { Id = _doctorId, FullName = "Old Name", IsActive = true, UserId = _userId };
        _doctorRepoMock.Setup(r => r.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var req = new UpdateDoctorRequest { FullName = "New Name", IsActive = false };

        // Act
        var result = await _service.UpdateDoctorAsync(_doctorId, req);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(existing.FullName, Is.EqualTo("New Name"));
        Assert.That(existing.IsActive, Is.False);

        _realtimeNotifierMock.Verify(n => n.TryNotifyDoctorRealtimeAccessChangedAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync($"doctors:{_doctorId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ UpdateDoctorStatusAsync & SoftDeleteDoctorAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task UpdateDoctorStatusAsync_ValidStatusChange_UpdatesStatusAndNotifies()
    {
        // Arrange
        var existing = new Doctor { Id = _doctorId, IsActive = true, UserId = _userId };
        _doctorRepoMock.Setup(r => r.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var req = new UpdateDoctorStatusRequest { IsActive = false };

        // Act
        var result = await _service.UpdateDoctorStatusAsync(_doctorId, req);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(existing.IsActive, Is.False);
        _realtimeNotifierMock.Verify(n => n.TryNotifyDoctorRealtimeAccessChangedAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task SoftDeleteDoctorAsync_ValidId_SoftDeletesDoctor()
    {
        // Arrange
        var existing = new Doctor { Id = _doctorId, IsActive = true, IsDeleted = false, UserId = _userId };
        _doctorRepoMock.Setup(r => r.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _service.SoftDeleteDoctorAsync(_doctorId);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(existing.IsDeleted, Is.True);
        Assert.That(existing.DeletedAt, Is.Not.Null);

        _realtimeNotifierMock.Verify(n => n.TryNotifyDoctorRealtimeAccessChangedAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

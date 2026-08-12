using System.Text.Json;
using AutoMapper;
using MedMateAI.Application.DTOs.MedicalFacilities.Requests;
using MedMateAI.Application.DTOs.MedicalFacilities.Responses;
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
public class MedicalFacilityServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IMedicalFacilityRepository> _facilityRepoMock = null!;
    private Mock<IMedicalDepartmentRepository> _departmentRepoMock = null!;
    private Mock<IFacilityDepartmentRepository> _facilityDepartmentRepoMock = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<IMapper> _mapperMock = null!;
    private MedicalFacilityService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _facilityRepoMock = new Mock<IMedicalFacilityRepository>();
        _departmentRepoMock = new Mock<IMedicalDepartmentRepository>();
        _facilityDepartmentRepoMock = new Mock<IFacilityDepartmentRepository>();
        _cacheMock = new Mock<IDistributedCache>();
        _mapperMock = new Mock<IMapper>();

        _unitOfWorkMock.Setup(u => u.MedicalFacilities).Returns(_facilityRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.MedicalDepartments).Returns(_departmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.FacilityDepartments).Returns(_facilityDepartmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mapperMock.Setup(m => m.Map<MedicalFacilityResponse>(It.IsAny<MedicalFacility>()))
            .Returns((MedicalFacility src) => new MedicalFacilityResponse
            {
                Id = src.Id,
                FacilityName = src.FacilityName,
                Address = src.Address,
                Latitude = src.Latitude,
                Longitude = src.Longitude,
                Phone = src.Phone,
                Website = src.Website,
                ImageUrl = src.ImageUrl,
                OpeningHours = src.OpeningHours,
                FacilityType = src.FacilityType,
                IsActive = src.IsActive,
                CreatedAt = src.CreatedAt,
                UpdatedAt = src.UpdatedAt,
            });

        _service = new MedicalFacilityService(_unitOfWorkMock.Object, _cacheMock.Object, _mapperMock.Object);

        _departmentRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalDepartment>());
        _facilityRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<MedicalFacility, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalFacility?)null);
        _facilityRepoMock.Setup(r => r.GetFacilityDepartmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FacilityDepartment>());
    }

    private void SetupCacheGetString(string key, string? value)
    {
        _cacheMock.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value is null ? null : System.Text.Encoding.UTF8.GetBytes(value));
    }

    private static MedicalFacility MakeFacility(Guid? id = null, bool isActive = true, bool isDeleted = false) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FacilityName = "Facility A",
        Address = "123 Main St",
        FacilityType = MedicalFacilityType.Hospital,
        IsActive = isActive,
        IsDeleted = isDeleted,
    };

    // â”€â”€ ListMedicalFacilitiesAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListMedicalFacilitiesAsync_ValidRequest_ReturnsPagedResponse()
    {
        var facility = MakeFacility();
        var pagedResult = new PagedResult<MedicalFacility>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1,
            Items = new List<MedicalFacility> { facility },
        };

        _facilityRepoMock.Setup(r => r.GetPagedWithDepartmentsAsync(1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _service.ListMedicalFacilitiesAsync(1, 10);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].FacilityName, Is.EqualTo("Facility A"));
    }

    // â”€â”€ ListActiveMedicalFacilitiesAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListActiveMedicalFacilitiesAsync_NoFilters_CacheHit_ReturnsCachedResponse()
    {
        var cached = new List<MedicalFacilityResponse> { new() { Id = Guid.NewGuid(), FacilityName = "Cached" } };
        SetupCacheGetString("medical-facilities:active", JsonSerializer.Serialize(cached));

        var result = await _service.ListActiveMedicalFacilitiesAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].FacilityName, Is.EqualTo("Cached"));
        _facilityRepoMock.Verify(r => r.GetActiveWithDepartmentsAsync(
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task ListActiveMedicalFacilitiesAsync_NoFilters_CacheMiss_QueriesDbAndSetsCache()
    {
        SetupCacheGetString("medical-facilities:active", null);
        _facilityRepoMock.Setup(r => r.GetActiveWithDepartmentsAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalFacility> { MakeFacility() });

        var result = await _service.ListActiveMedicalFacilitiesAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        _cacheMock.Verify(c => c.SetAsync(
            "medical-facilities:active",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task ListActiveMedicalFacilitiesAsync_WithDepartmentFilter_BypassesCache()
    {
        var departmentId = Guid.NewGuid();
        _facilityRepoMock.Setup(r => r.GetActiveWithDepartmentsAsync(departmentId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalFacility> { MakeFacility() });

        var result = await _service.ListActiveMedicalFacilitiesAsync(departmentId);

        Assert.That(result, Has.Count.EqualTo(1));
        _cacheMock.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // â”€â”€ GetMedicalFacilityByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetMedicalFacilityByIdAsync_EmptyId_ReturnsNull()
    {
        Assert.That(await _service.GetMedicalFacilityByIdAsync(Guid.Empty), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetMedicalFacilityByIdAsync_CacheHit_ReturnsCachedResponse()
    {
        var id = Guid.NewGuid();
        var cached = new MedicalFacilityResponse { Id = id, FacilityName = "Cached Facility" };
        SetupCacheGetString($"medical-facilities:{id}", JsonSerializer.Serialize(cached));

        var result = await _service.GetMedicalFacilityByIdAsync(id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.FacilityName, Is.EqualTo("Cached Facility"));
        _facilityRepoMock.Verify(r => r.GetByIdWithDepartmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task GetMedicalFacilityByIdAsync_CacheMissNotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        SetupCacheGetString($"medical-facilities:{id}", null);
        _facilityRepoMock.Setup(r => r.GetByIdWithDepartmentsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalFacility?)null);

        Assert.That(await _service.GetMedicalFacilityByIdAsync(id), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetMedicalFacilityByIdAsync_CacheMissFound_QueriesDbAndSetsCache()
    {
        var id = Guid.NewGuid();
        SetupCacheGetString($"medical-facilities:{id}", null);
        _facilityRepoMock.Setup(r => r.GetByIdWithDepartmentsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeFacility(id));

        var result = await _service.GetMedicalFacilityByIdAsync(id);

        Assert.That(result, Is.Not.Null);
        _cacheMock.Verify(c => c.SetAsync(
            $"medical-facilities:{id}",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ CreateMedicalFacilityAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task CreateMedicalFacilityAsync_NullRequest_ReturnsError()
    {
        var (succeeded, errors, data) = await _service.CreateMedicalFacilityAsync(null!);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Request body là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task CreateMedicalFacilityAsync_InvalidInputs_ReturnsErrors()
    {
        var request = new CreateMedicalFacilityRequest
        {
            FacilityName = " ",
            Latitude = 200m,
            Longitude = -200m,
            Website = "not-a-url",
            ImageUrl = "ftp://bad.com/x.jpg",
            DepartmentIds = new List<Guid> { Guid.Empty },
        };

        var (succeeded, errors, data) = await _service.CreateMedicalFacilityAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Tên cơ sở y tế là bắt buộc"));
        Assert.That(errors, Contains.Item("Latitude phải từ -90 đến 90"));
        Assert.That(errors, Contains.Item("Longitude phải từ -180 đến 180"));
        Assert.That(errors, Contains.Item("Website không hợp lệ"));
        Assert.That(errors, Contains.Item("ImageUrl không hợp lệ"));
        Assert.That(errors, Contains.Item("DepartmentIds chứa Guid rỗng"));
    }

    [Test]
    [Category("A")]
    public async Task CreateMedicalFacilityAsync_DepartmentIdsNotFound_ReturnsError()
    {
        var departmentId = Guid.NewGuid();
        var request = new CreateMedicalFacilityRequest
        {
            FacilityName = "Facility A",
            DepartmentIds = new List<Guid> { departmentId },
        };

        _departmentRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalDepartment>());

        var (succeeded, errors, data) = await _service.CreateMedicalFacilityAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Một số DepartmentId không tồn tại hoặc đã xóa"));
    }

    [Test]
    [Category("A")]
    public async Task CreateMedicalFacilityAsync_DuplicateFacility_ReturnsError()
    {
        var request = new CreateMedicalFacilityRequest { FacilityName = "Facility A", Address = "123 Main St" };

        _facilityRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<MedicalFacility, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeFacility());

        var (succeeded, errors, data) = await _service.CreateMedicalFacilityAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Cơ sở y tế cùng tên và địa chỉ đã tồn tại"));
    }

    [Test]
    [Category("N")]
    public async Task CreateMedicalFacilityAsync_ValidRequest_CreatesAndInvalidatesCache()
    {
        var departmentId = Guid.NewGuid();
        var request = new CreateMedicalFacilityRequest
        {
            FacilityName = " Facility A ",
            Address = " 123 Main St ",
            Website = "https://facility-a.example.com",
            ImageUrl = "https://facility-a.example.com/logo.jpg",
            DepartmentIds = new List<Guid> { departmentId },
        };

        _departmentRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalDepartment> { new() { Id = departmentId, IsDeleted = false } });

        MedicalFacility? captured = null;
        _facilityRepoMock.Setup(r => r.Add(It.IsAny<MedicalFacility>()))
            .Callback<MedicalFacility>(f => captured = f);
        _facilityRepoMock.Setup(r => r.GetByIdWithDepartmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => captured);

        var (succeeded, errors, data) = await _service.CreateMedicalFacilityAsync(request);

        Assert.That(succeeded, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.FacilityName, Is.EqualTo("Facility A"));
        _facilityRepoMock.Verify(r => r.Add(It.IsAny<MedicalFacility>()), Times.Once);
        _facilityDepartmentRepoMock.Verify(r => r.Add(It.IsAny<FacilityDepartment>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync("medical-facilities:active", It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ UpdateMedicalFacilityAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdateMedicalFacilityAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityAsync(
            Guid.Empty, new UpdateMedicalFacilityRequest());

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Id cơ sở y tế không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateMedicalFacilityAsync_NullRequest_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityAsync(Guid.NewGuid(), null!);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Request body là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateMedicalFacilityAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalFacility?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityAsync(
            id, new UpdateMedicalFacilityRequest { FacilityName = "New" });

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateMedicalFacilityAsync_InvalidFacilityType_ReturnsError()
    {
        var id = Guid.NewGuid();
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeFacility(id));

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityAsync(
            id, new UpdateMedicalFacilityRequest { FacilityType = (MedicalFacilityType)999 });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("FacilityType không hợp lệ"));
    }

    [Test]
    [Category("B")]
    public async Task UpdateMedicalFacilityAsync_BlankFacilityName_ReturnsError()
    {
        var id = Guid.NewGuid();
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeFacility(id));

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityAsync(
            id, new UpdateMedicalFacilityRequest { FacilityName = " " });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Tên cơ sở y tế không được để trống"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateMedicalFacilityAsync_DuplicateDepartmentIds_ReturnsError()
    {
        var id = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeFacility(id));

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityAsync(
            id, new UpdateMedicalFacilityRequest { DepartmentIds = new List<Guid> { departmentId, departmentId } });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("DepartmentIds phải là các giá trị khác nhau"));
    }

    [Test]
    [Category("N")]
    public async Task UpdateMedicalFacilityAsync_ValidRequest_UpdatesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var existing = MakeFacility(id);
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _facilityRepoMock.Setup(r => r.GetByIdWithDepartmentsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateMedicalFacilityRequest { FacilityName = "New Name", IsActive = false };

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityAsync(id, request);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.FacilityName, Is.EqualTo("New Name"));
        Assert.That(existing.IsActive, Is.False);
        _facilityRepoMock.Verify(r => r.Update(existing), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync("medical-facilities:active", It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync($"medical-facilities:{id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task UpdateMedicalFacilityAsync_ReplacesDepartments_AddsAndRemovesAsNeeded()
    {
        var id = Guid.NewGuid();
        var existing = MakeFacility(id);
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _facilityRepoMock.Setup(r => r.GetByIdWithDepartmentsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var keepDepartmentId = Guid.NewGuid();
        var removeDepartmentId = Guid.NewGuid();
        var addDepartmentId = Guid.NewGuid();

        var keepFd = new FacilityDepartment { Id = Guid.NewGuid(), FacilityId = id, DepartmentId = keepDepartmentId, IsDeleted = false };
        var removeFd = new FacilityDepartment { Id = Guid.NewGuid(), FacilityId = id, DepartmentId = removeDepartmentId, IsDeleted = false };
        _facilityRepoMock.Setup(r => r.GetFacilityDepartmentsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FacilityDepartment> { keepFd, removeFd });

        _departmentRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalDepartment>
            {
                new() { Id = keepDepartmentId, IsDeleted = false },
                new() { Id = addDepartmentId, IsDeleted = false },
            });

        var request = new UpdateMedicalFacilityRequest
        {
            DepartmentIds = new List<Guid> { keepDepartmentId, addDepartmentId },
        };

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityAsync(id, request);

        Assert.That(succeeded, Is.True);
        Assert.That(removeFd.IsDeleted, Is.True);
        Assert.That(keepFd.IsDeleted, Is.False);
        _facilityDepartmentRepoMock.Verify(r => r.Add(It.Is<FacilityDepartment>(fd => fd.DepartmentId == addDepartmentId)), Times.Once);
        _facilityDepartmentRepoMock.Verify(r => r.Update(removeFd), Times.Once);
    }

    // â”€â”€ UpdateMedicalFacilityStatusAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdateMedicalFacilityStatusAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityStatusAsync(
            Guid.Empty, new UpdateMedicalFacilityStatusRequest { IsActive = true });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Id cơ sở y tế không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateMedicalFacilityStatusAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalFacility?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityStatusAsync(
            id, new UpdateMedicalFacilityStatusRequest { IsActive = false });

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task UpdateMedicalFacilityStatusAsync_ValidRequest_UpdatesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var existing = MakeFacility(id, isActive: true);
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _facilityRepoMock.Setup(r => r.GetByIdWithDepartmentsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalFacilityStatusAsync(
            id, new UpdateMedicalFacilityStatusRequest { IsActive = false });

        Assert.That(succeeded, Is.True);
        Assert.That(existing.IsActive, Is.False);
        _cacheMock.Verify(c => c.RemoveAsync($"medical-facilities:{id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ SoftDeleteMedicalFacilityAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task SoftDeleteMedicalFacilityAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors) = await _service.SoftDeleteMedicalFacilityAsync(Guid.Empty);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Id cơ sở y tế không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteMedicalFacilityAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalFacility?)null);

        var (succeeded, notFound, errors) = await _service.SoftDeleteMedicalFacilityAsync(id);

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task SoftDeleteMedicalFacilityAsync_ValidId_SoftDeletesFacilityAndDepartments()
    {
        var id = Guid.NewGuid();
        var existing = MakeFacility(id);
        _facilityRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var fd = new FacilityDepartment { Id = Guid.NewGuid(), FacilityId = id, DepartmentId = Guid.NewGuid(), IsDeleted = false };
        _facilityRepoMock.Setup(r => r.GetFacilityDepartmentsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FacilityDepartment> { fd });

        var (succeeded, notFound, errors) = await _service.SoftDeleteMedicalFacilityAsync(id);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.IsDeleted, Is.True);
        Assert.That(fd.IsDeleted, Is.True);
        _facilityRepoMock.Verify(r => r.Update(existing), Times.Once);
        _facilityDepartmentRepoMock.Verify(r => r.Update(fd), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync("medical-facilities:active", It.IsAny<CancellationToken>()), Times.Once);
    }
}

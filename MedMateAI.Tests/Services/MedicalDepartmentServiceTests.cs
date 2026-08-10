using System.Linq.Expressions;
using System.Text.Json;
using MedMateAI.Application.DTOs.MedicalDepartments.Requests;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class MedicalDepartmentServiceTests
{
    private Mock<IGenericRepository<MedicalDepartment>> _departmentRepoMock = null!;
    private Mock<IGenericRepository<IcdChapter>> _chapterRepoMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<ILogger<MedicalDepartmentService>> _loggerMock = null!;
    private MedicalDepartmentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _departmentRepoMock = new Mock<IGenericRepository<MedicalDepartment>>();
        _chapterRepoMock = new Mock<IGenericRepository<IcdChapter>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<MedicalDepartmentService>>();

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _service = new MedicalDepartmentService(
            _departmentRepoMock.Object,
            _chapterRepoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    private void SetupCacheGetString(string key, string? value)
    {
        _cacheMock.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value is null ? null : System.Text.Encoding.UTF8.GetBytes(value));
    }

    // â”€â”€ ListMedicalDepartmentsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListMedicalDepartmentsAsync_CacheHit_ReturnsCachedResponse()
    {
        var cachedList = new List<MedMateAI.Application.DTOs.MedicalDepartments.Responses.MedicalDepartmentResponse>
        {
            new() { Id = Guid.NewGuid(), DepartmentName = "Cached Dept" },
        };
        SetupCacheGetString("medical-departments:all", JsonSerializer.Serialize(cachedList));

        var result = await _service.ListMedicalDepartmentsAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].DepartmentName, Is.EqualTo("Cached Dept"));
        _departmentRepoMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task ListMedicalDepartmentsAsync_CacheMiss_QueriesDbAndSetsCache()
    {
        SetupCacheGetString("medical-departments:all", null);

        var entities = new List<MedicalDepartment>
        {
            new() { Id = Guid.NewGuid(), DepartmentName = "Cardiology", IsDeleted = false },
            new() { Id = Guid.NewGuid(), DepartmentName = "Deleted Dept", IsDeleted = true },
        };
        _departmentRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entities);

        var result = await _service.ListMedicalDepartmentsAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].DepartmentName, Is.EqualTo("Cardiology"));
        _cacheMock.Verify(c => c.SetAsync(
            "medical-departments:all",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task ListMedicalDepartmentsAsync_CacheReadThrows_FallsBackToDatabase()
    {
        _cacheMock.Setup(c => c.GetAsync("medical-departments:all", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis down"));

        var entities = new List<MedicalDepartment>
        {
            new() { Id = Guid.NewGuid(), DepartmentName = "Neurology", IsDeleted = false },
        };
        _departmentRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entities);

        var result = await _service.ListMedicalDepartmentsAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].DepartmentName, Is.EqualTo("Neurology"));
    }

    [Test]
    [Category("A")]
    public async Task ListMedicalDepartmentsAsync_CacheWriteThrows_StillReturnsDbResult()
    {
        SetupCacheGetString("medical-departments:all", null);

        var entities = new List<MedicalDepartment>
        {
            new() { Id = Guid.NewGuid(), DepartmentName = "Dermatology", IsDeleted = false },
        };
        _departmentRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entities);

        _cacheMock.Setup(c => c.SetAsync(
                "medical-departments:all",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis write failed"));

        var result = await _service.ListMedicalDepartmentsAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].DepartmentName, Is.EqualTo("Dermatology"));
    }

    // â”€â”€ GetMedicalDepartmentByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetMedicalDepartmentByIdAsync_EmptyId_ReturnsNull()
    {
        Assert.That(await _service.GetMedicalDepartmentByIdAsync(Guid.Empty), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetMedicalDepartmentByIdAsync_CacheHit_ReturnsCachedResponse()
    {
        var id = Guid.NewGuid();
        var cached = new MedMateAI.Application.DTOs.MedicalDepartments.Responses.MedicalDepartmentResponse
        {
            Id = id,
            DepartmentName = "Cached Dept",
        };
        SetupCacheGetString($"medical-departments:{id}", JsonSerializer.Serialize(cached));

        var result = await _service.GetMedicalDepartmentByIdAsync(id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DepartmentName, Is.EqualTo("Cached Dept"));
        _departmentRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task GetMedicalDepartmentByIdAsync_CacheMissNotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        SetupCacheGetString($"medical-departments:{id}", null);
        _departmentRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalDepartment?)null);

        Assert.That(await _service.GetMedicalDepartmentByIdAsync(id), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetMedicalDepartmentByIdAsync_CacheMissDeleted_ReturnsNull()
    {
        var id = Guid.NewGuid();
        SetupCacheGetString($"medical-departments:{id}", null);
        _departmentRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalDepartment { Id = id, IsDeleted = true });

        Assert.That(await _service.GetMedicalDepartmentByIdAsync(id), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetMedicalDepartmentByIdAsync_CacheMissFound_QueriesDbAndSetsCache()
    {
        var id = Guid.NewGuid();
        SetupCacheGetString($"medical-departments:{id}", null);
        _departmentRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalDepartment { Id = id, DepartmentName = "Oncology", IsDeleted = false });

        var result = await _service.GetMedicalDepartmentByIdAsync(id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DepartmentName, Is.EqualTo("Oncology"));
        _cacheMock.Verify(c => c.SetAsync(
            $"medical-departments:{id}",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ CreateMedicalDepartmentAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task CreateMedicalDepartmentAsync_EmptyName_ReturnsError()
    {
        var request = new CreateMedicalDepartmentRequest { DepartmentName = " " };

        var (succeeded, errors, data) = await _service.CreateMedicalDepartmentAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Department name is required."));
    }

    [Test]
    [Category("A")]
    public async Task CreateMedicalDepartmentAsync_ChapterCodeNotFound_ReturnsError()
    {
        var request = new CreateMedicalDepartmentRequest { DepartmentName = "Cardiology", ChapterCode = "Z99" };

        _chapterRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        var (succeeded, errors, data) = await _service.CreateMedicalDepartmentAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("ICD chapter 'Z99' was not found."));
    }

    [Test]
    [Category("N")]
    public async Task CreateMedicalDepartmentAsync_ValidRequest_CreatesAndInvalidatesCache()
    {
        var request = new CreateMedicalDepartmentRequest
        {
            DepartmentName = " Cardiology ",
            Description = "Heart care",
            ChapterCode = "a00",
        };

        _chapterRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IcdChapter { ChapterCode = "A00" });

        var (succeeded, errors, data) = await _service.CreateMedicalDepartmentAsync(request);

        Assert.That(succeeded, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.DepartmentName, Is.EqualTo("Cardiology"));
        Assert.That(data.ChapterCode, Is.EqualTo("A00"));
        _departmentRepoMock.Verify(r => r.Add(It.IsAny<MedicalDepartment>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync("medical-departments:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ UpdateMedicalDepartmentAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdateMedicalDepartmentAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalDepartmentAsync(
            Guid.Empty, new UpdateMedicalDepartmentRequest { DepartmentName = "x" });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Invalid medical department id."));
    }

    [Test]
    [Category("B")]
    public async Task UpdateMedicalDepartmentAsync_BlankName_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalDepartmentAsync(
            Guid.NewGuid(), new UpdateMedicalDepartmentRequest { DepartmentName = " " });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Department name cannot be empty when provided."));
    }

    [Test]
    [Category("A")]
    public async Task UpdateMedicalDepartmentAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _departmentRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalDepartment?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalDepartmentAsync(
            id, new UpdateMedicalDepartmentRequest { DepartmentName = "New" });

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateMedicalDepartmentAsync_NewChapterCodeNotFound_ReturnsError()
    {
        var id = Guid.NewGuid();
        _departmentRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalDepartment { Id = id, DepartmentName = "Old", IsDeleted = false });

        _chapterRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalDepartmentAsync(
            id, new UpdateMedicalDepartmentRequest { ChapterCode = "Z99" });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("ICD chapter 'Z99' was not found."));
    }

    [Test]
    [Category("N")]
    public async Task UpdateMedicalDepartmentAsync_ValidRequest_UpdatesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var existing = new MedicalDepartment { Id = id, DepartmentName = "Old", IsDeleted = false };
        _departmentRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateMedicalDepartmentRequest { DepartmentName = "New Name", Description = "Updated" };

        var (succeeded, notFound, errors, data) = await _service.UpdateMedicalDepartmentAsync(id, request);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.DepartmentName, Is.EqualTo("New Name"));
        Assert.That(existing.Description, Is.EqualTo("Updated"));
        _departmentRepoMock.Verify(r => r.Update(existing), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync("medical-departments:all", It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync($"medical-departments:{id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ SoftDeleteMedicalDepartmentAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task SoftDeleteMedicalDepartmentAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors) = await _service.SoftDeleteMedicalDepartmentAsync(Guid.Empty);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Invalid medical department id."));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteMedicalDepartmentAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _departmentRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalDepartment?)null);

        var (succeeded, notFound, errors) = await _service.SoftDeleteMedicalDepartmentAsync(id);

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task SoftDeleteMedicalDepartmentAsync_ValidId_SoftDeletesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var existing = new MedicalDepartment { Id = id, IsDeleted = false };
        _departmentRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var (succeeded, notFound, errors) = await _service.SoftDeleteMedicalDepartmentAsync(id);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.IsDeleted, Is.True);
        Assert.That(existing.DeletedAt, Is.Not.Null);
        _departmentRepoMock.Verify(r => r.Update(existing), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync($"medical-departments:{id}", It.IsAny<CancellationToken>()), Times.Once);
    }
}

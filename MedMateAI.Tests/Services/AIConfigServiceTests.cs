using System.Linq.Expressions;
using MedMateAI.Application.DTOs.AIConfigs.Requests;
using MedMateAI.Application.Models;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class AIConfigServiceTests
{
    private Mock<IGenericRepository<AISystemConfig>> _repoMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private AIConfigService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repoMock = new Mock<IGenericRepository<AISystemConfig>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new AIConfigService(_repoMock.Object, _unitOfWorkMock.Object);
    }

    [Test]
    [Category("N")]
    public async Task ListAIConfigsAsync_ValidPage_ReturnsPagedAIConfigs()
    {
        // Arrange
        var pagedResult = new PagedResult<AISystemConfig>
        {
            Items = new List<AISystemConfig> { new() { TaskType = "A" } },
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1
        };

        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<AISystemConfig, bool>>>(),
                It.IsAny<Func<IQueryable<AISystemConfig>, IOrderedQueryable<AISystemConfig>>>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _service.ListAIConfigsAsync(1, 10, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].TaskType, Is.EqualTo("A"));
    }

    [Test]
    [Category("N")]
    public async Task ListActiveAIConfigsAsync_Always_ReturnsActiveAIConfigsOrderedByTaskType()
    {
        // Arrange
        var list = new List<AISystemConfig>
        {
            new() { TaskType = "B", IsActive = true, IsDeleted = false },
            new() { TaskType = "A", IsActive = true, IsDeleted = false },
            new() { TaskType = "C", IsActive = false, IsDeleted = false }, // Inactive
            new() { TaskType = "D", IsActive = true, IsDeleted = true } // Deleted
        };

        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        // Act
        var result = await _service.ListActiveAIConfigsAsync(CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].TaskType, Is.EqualTo("A"));
        Assert.That(result[1].TaskType, Is.EqualTo("B"));
    }

    [Test]
    [Category("B")]
    public async Task GetAIConfigByIdAsync_EmptyId_ReturnsNull()
    {
        var result = await _service.GetAIConfigByIdAsync(Guid.Empty, CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetAIConfigByIdAsync_FoundAndNotDeleted_ReturnsResponse()
    {
        // Arrange
        var id = Guid.NewGuid();
        var config = new AISystemConfig { Id = id, TaskType = "Test", IsDeleted = false };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        // Act
        var result = await _service.GetAIConfigByIdAsync(id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(id));
    }

    [Test]
    [Category("A")]
    public async Task GetAIConfigByIdAsync_NotFoundOrDeleted_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AISystemConfig?)null);

        // Act
        var result = await _service.GetAIConfigByIdAsync(id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("B")]
    public async Task GetActiveAIConfigByTaskTypeAsync_EmptyTaskType_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.GetActiveAIConfigByTaskTypeAsync("", CancellationToken.None));
    }

    [Test]
    [Category("N")]
    public async Task GetActiveAIConfigByTaskTypeAsync_Found_ReturnsResponse()
    {
        // Arrange
        var taskType = "Review";
        var config = new AISystemConfig { TaskType = taskType, IsActive = true, IsDeleted = false };
        _repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<AISystemConfig, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _service.GetActiveAIConfigByTaskTypeAsync(taskType, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TaskType, Is.EqualTo(taskType));
    }

    [Test]
    [Category("A")]
    public async Task CreateAIConfigAsync_NullRequest_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAIConfigAsync(null!, CancellationToken.None));
    }

    [Test]
    [Category("B")]
    public async Task CreateAIConfigAsync_EmptyTaskType_ThrowsArgumentException()
    {
        var req = new CreateAIConfigRequest { TaskType = "" };
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAIConfigAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("B")]
    public async Task CreateAIConfigAsync_InvalidTemperature_ThrowsArgumentException()
    {
        var req = new CreateAIConfigRequest { TaskType = "Test", Temperature = 2.5m };
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAIConfigAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("B")]
    public async Task CreateAIConfigAsync_InvalidMaxTokens_ThrowsArgumentException()
    {
        var req = new CreateAIConfigRequest { TaskType = "Test", MaxTokens = 0 };
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAIConfigAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("A")]
    public async Task CreateAIConfigAsync_DuplicateTaskType_ThrowsInvalidOperationException()
    {
        // Arrange
        var req = new CreateAIConfigRequest { TaskType = "Duplicate" };
        var duplicate = new AISystemConfig { TaskType = "Duplicate", IsDeleted = false };
        _repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<AISystemConfig, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicate);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAIConfigAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("N")]
    public async Task CreateAIConfigAsync_ValidRequest_CreatesAIConfig()
    {
        // Arrange
        var req = new CreateAIConfigRequest { TaskType = "Unique", Temperature = 1.0m, MaxTokens = 100 };
        _repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<AISystemConfig, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AISystemConfig?)null);

        // Act
        var result = await _service.CreateAIConfigAsync(req, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.TaskType, Is.EqualTo("Unique"));
        _repoMock.Verify(r => r.Add(It.IsAny<AISystemConfig>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("B")]
    public async Task UpdateAIConfigAsync_EmptyId_ReturnsNull()
    {
        var result = await _service.UpdateAIConfigAsync(Guid.Empty, new UpdateAIConfigRequest(), CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task UpdateAIConfigAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AISystemConfig?)null);

        // Act
        var result = await _service.UpdateAIConfigAsync(id, new UpdateAIConfigRequest(), CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("B")]
    public async Task UpdateAIConfigAsync_EmptyTaskType_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var config = new AISystemConfig { Id = id, TaskType = "Old" };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var req = new UpdateAIConfigRequest { TaskType = "" };

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAIConfigAsync(id, req, CancellationToken.None));
    }

    [Test]
    [Category("A")]
    public async Task UpdateAIConfigAsync_DuplicateTaskType_ThrowsInvalidOperationException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var config = new AISystemConfig { Id = id, TaskType = "Old" };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var req = new UpdateAIConfigRequest { TaskType = "Duplicate" };
        var duplicate = new AISystemConfig { Id = Guid.NewGuid(), TaskType = "Duplicate", IsDeleted = false };
        _repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<AISystemConfig, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicate);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAIConfigAsync(id, req, CancellationToken.None));
    }

    [Test]
    [Category("N")]
    public async Task UpdateAIConfigAsync_ValidRequest_UpdatesAIConfig()
    {
        // Arrange
        var id = Guid.NewGuid();
        var config = new AISystemConfig { Id = id, TaskType = "Old", Temperature = 0.5m, MaxTokens = 50 };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var req = new UpdateAIConfigRequest { TaskType = "New", Temperature = 1.5m, MaxTokens = 150 };
        _repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<AISystemConfig, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AISystemConfig?)null);

        // Act
        var result = await _service.UpdateAIConfigAsync(id, req, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(config.TaskType, Is.EqualTo("New"));
        Assert.That(config.Temperature, Is.EqualTo(1.5m));
        Assert.That(config.MaxTokens, Is.EqualTo(150));
        _repoMock.Verify(r => r.Update(config), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task UpdateAIConfigStatusAsync_ValidRequest_UpdatesStatus()
    {
        // Arrange
        var id = Guid.NewGuid();
        var config = new AISystemConfig { Id = id, IsActive = false };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var req = new UpdateAIConfigStatusRequest { IsActive = true };

        // Act
        var result = await _service.UpdateAIConfigStatusAsync(id, req, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(config.IsActive, Is.True);
        _repoMock.Verify(r => r.Update(config), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task DeleteAIConfigAsync_Found_SoftDeletesAndReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var config = new AISystemConfig { Id = id, IsDeleted = false };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        // Act
        var result = await _service.DeleteAIConfigAsync(id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(config.IsDeleted, Is.True);
        _repoMock.Verify(r => r.Update(config), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task DeleteAIConfigAsync_NotFound_ReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AISystemConfig?)null);

        // Act
        var result = await _service.DeleteAIConfigAsync(id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.False);
    }
}

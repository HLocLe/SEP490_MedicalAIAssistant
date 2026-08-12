using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Responses;
using MedMateAI.Application.DTOs.SubscriptionPlans.Requests;
using MedMateAI.Application.DTOs.SubscriptionPlans.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class SubscriptionPlanServiceTests
{
    private Mock<IGenericRepository<SubscriptionPlan>> _repoMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<ISubscriptionPlanQuotaService> _quotaServiceMock = null!;
    private Mock<ISubscriptionPlanCacheInvalidator> _cacheInvalidatorMock = null!;
    private SubscriptionPlanService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repoMock = new Mock<IGenericRepository<SubscriptionPlan>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheMock = new Mock<IDistributedCache>();
        _quotaServiceMock = new Mock<ISubscriptionPlanQuotaService>();
        _cacheInvalidatorMock = new Mock<ISubscriptionPlanCacheInvalidator>();
        _cacheInvalidatorMock
            .Setup(c => c.InvalidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _quotaServiceMock.Setup(service => service.GetActivePlanQuotasAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<SubscriptionPlanQuotaResponse>>());
        _service = new SubscriptionPlanService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            _quotaServiceMock.Object,
            _cacheInvalidatorMock.Object);
    }

    [Test]
    [Category("N")]
    public async Task ListSubscriptionPlansAsync_CacheHit_ReturnsCachedPlans()
    {
        // Arrange
        var cacheKey = "subscription-plans:all";
        var cachedPlans = new List<SubscriptionPlanResponse>
        {
            new() { Id = Guid.NewGuid(), PlanName = "Cached Plan", Price = 100 }
        };
        var cachedData = JsonSerializer.Serialize(cachedPlans);
        _cacheMock.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cachedData));

        // Act
        var result = await _service.ListSubscriptionPlansAsync(CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PlanName, Is.EqualTo("Cached Plan"));
        _repoMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task ListSubscriptionPlansAsync_CacheMiss_RetrievesAndCachesPlans()
    {
        // Arrange
        var cacheKey = "subscription-plans:all";
        _cacheMock.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var entities = new List<SubscriptionPlan>
        {
            new() { Id = Guid.NewGuid(), PlanName = "DB Plan", Price = 100, IsDeleted = false }
        };
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _service.ListSubscriptionPlansAsync(CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PlanName, Is.EqualTo("DB Plan"));
        _cacheMock.Verify(c => c.SetAsync(
            cacheKey,
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task ListActiveSubscriptionPlansAsync_CacheHit_ReturnsCachedActivePlans()
    {
        // Arrange
        var cacheKey = "subscription-plans:active";
        var cachedPlans = new List<SubscriptionPlanResponse>
        {
            new() { Id = Guid.NewGuid(), PlanName = "Cached Active Plan", Price = 100 }
        };
        var cachedData = JsonSerializer.Serialize(cachedPlans);
        _cacheMock.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cachedData));

        // Act
        var result = await _service.ListActiveSubscriptionPlansAsync(CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PlanName, Is.EqualTo("Cached Active Plan"));
    }

    [Test]
    [Category("N")]
    public async Task ListActiveSubscriptionPlansAsync_CacheMiss_RetrievesAndCachesActivePlans()
    {
        // Arrange
        var cacheKey = "subscription-plans:active";
        _cacheMock.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var entities = new List<SubscriptionPlan>
        {
            new() { Id = Guid.NewGuid(), PlanName = "Plan A", Price = 50, IsActive = true, IsDeleted = false },
            new() { Id = Guid.NewGuid(), PlanName = "Plan B", Price = 100, IsActive = false, IsDeleted = false } // Inactive
        };
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _service.ListActiveSubscriptionPlansAsync(CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PlanName, Is.EqualTo("Plan A"));
        _cacheMock.Verify(c => c.SetAsync(
            cacheKey,
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("B")]
    public async Task GetSubscriptionPlanByIdAsync_EmptyId_ReturnsNull()
    {
        var result = await _service.GetSubscriptionPlanByIdAsync(Guid.Empty, CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetSubscriptionPlanByIdAsync_CacheHit_ReturnsCachedPlan()
    {
        // Arrange
        var id = Guid.NewGuid();
        var cacheKey = $"subscription-plans:{id}";
        var cachedPlan = new SubscriptionPlanResponse { Id = id, PlanName = "Cached" };
        var cachedData = JsonSerializer.Serialize(cachedPlan);
        _cacheMock.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cachedData));

        // Act
        var result = await _service.GetSubscriptionPlanByIdAsync(id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PlanName, Is.EqualTo("Cached"));
    }

    [Test]
    [Category("A")]
    public async Task GetSubscriptionPlanByIdAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        var cacheKey = $"subscription-plans:{id}";
        _cacheMock.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SubscriptionPlan?)null);

        // Act
        var result = await _service.GetSubscriptionPlanByIdAsync(id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task CreateSubscriptionPlanAsync_NullRequest_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateSubscriptionPlanAsync(null!, CancellationToken.None));
    }

    [Test]
    [Category("B")]
    public async Task CreateSubscriptionPlanAsync_EmptyPlanName_ThrowsArgumentException()
    {
        var req = new CreateSubscriptionPlanRequest { PlanName = "" };
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateSubscriptionPlanAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("B")]
    public async Task CreateSubscriptionPlanAsync_NegativePrice_ThrowsArgumentException()
    {
        var req = new CreateSubscriptionPlanRequest { PlanName = "A", Price = -1 };
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateSubscriptionPlanAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("B")]
    public async Task CreateSubscriptionPlanAsync_NonPositiveDuration_ThrowsArgumentException()
    {
        var req = new CreateSubscriptionPlanRequest { PlanName = "A", Price = 10, DurationInDays = 0 };
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateSubscriptionPlanAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("A")]
    public async Task CreateSubscriptionPlanAsync_InvalidJsonFeatureLimit_ThrowsArgumentException()
    {
        var req = new CreateSubscriptionPlanRequest { PlanName = "A", Price = 10, DurationInDays = 30, FeatureLimitJson = "invalid-json" };
        Assert.ThrowsAsync<ArgumentException>(() => _service.CreateSubscriptionPlanAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("A")]
    public async Task CreateSubscriptionPlanAsync_DuplicatePlanName_ThrowsInvalidOperationException()
    {
        // Arrange
        var req = new CreateSubscriptionPlanRequest { PlanName = "Duplicate", Price = 10, DurationInDays = 30 };
        var duplicate = new SubscriptionPlan { PlanName = "Duplicate", IsDeleted = false };
        _repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicate);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateSubscriptionPlanAsync(req, CancellationToken.None));
    }

    [Test]
    [Category("N")]
    public async Task CreateSubscriptionPlanAsync_ValidRequest_CreatesPlanAndInvalidatesCache()
    {
        // Arrange
        var req = new CreateSubscriptionPlanRequest { PlanName = "Unique", Price = 10, DurationInDays = 30, FeatureLimitJson = "{}" };
        _repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        // Act
        var result = await _service.CreateSubscriptionPlanAsync(req, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PlanName, Is.EqualTo("Unique"));
        _repoMock.Verify(r => r.Add(It.IsAny<SubscriptionPlan>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(c => c.InvalidateAsync(
            result.Id, CancellationToken.None), Times.Once);
    }

    [Test]
    [Category("B")]
    public async Task UpdateSubscriptionPlanAsync_EmptyId_ReturnsNull()
    {
        var result = await _service.UpdateSubscriptionPlanAsync(Guid.Empty, new UpdateSubscriptionPlanRequest(), CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task UpdateSubscriptionPlanAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SubscriptionPlan?)null);

        // Act
        var result = await _service.UpdateSubscriptionPlanAsync(id, new UpdateSubscriptionPlanRequest(), CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task UpdateSubscriptionPlanAsync_ValidRequest_UpdatesPlanAndInvalidatesCache()
    {
        // Arrange
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan { Id = id, PlanName = "Old", Price = 10, DurationInDays = 30 };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var req = new UpdateSubscriptionPlanRequest { PlanName = "New", Price = 20, DurationInDays = 60, FeatureLimitJson = "{}" };
        _repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        // Act
        var result = await _service.UpdateSubscriptionPlanAsync(id, req, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(plan.PlanName, Is.EqualTo("New"));
        Assert.That(plan.Price, Is.EqualTo(20));
        Assert.That(plan.DurationInDays, Is.EqualTo(60));
        _repoMock.Verify(r => r.Update(plan), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(c => c.InvalidateAsync(
            id, CancellationToken.None), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task UpdateSubscriptionPlanStatusAsync_ValidRequest_UpdatesStatus()
    {
        // Arrange
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan { Id = id, IsActive = false };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var req = new UpdateSubscriptionPlanStatusRequest { IsActive = true };

        // Act
        var result = await _service.UpdateSubscriptionPlanStatusAsync(id, req, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(plan.IsActive, Is.True);
        _repoMock.Verify(r => r.Update(plan), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(c => c.InvalidateAsync(
            id, CancellationToken.None), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task DeleteSubscriptionPlanAsync_Found_SoftDeletesPlanAndInvalidatesCache()
    {
        // Arrange
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan { Id = id, IsDeleted = false };
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        // Act
        var result = await _service.DeleteSubscriptionPlanAsync(id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(plan.IsDeleted, Is.True);
        _repoMock.Verify(r => r.Update(plan), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(c => c.InvalidateAsync(
            id, CancellationToken.None), Times.Once);
    }
}

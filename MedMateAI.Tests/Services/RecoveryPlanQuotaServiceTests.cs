using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.Models;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanQuotaServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IUserSubscriptionRepository> _userSubscriptionsMock = null!;
    private Mock<IQuotaUsageRepository> _quotaUsagesMock = null!;
    private RecoveryPlanQuotaService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userSubscriptionsMock = new Mock<IUserSubscriptionRepository>();
        _quotaUsagesMock = new Mock<IQuotaUsageRepository>();

        _unitOfWorkMock.Setup(u => u.UserSubscriptions).Returns(_userSubscriptionsMock.Object);
        _unitOfWorkMock.Setup(u => u.QuotaUsages).Returns(_quotaUsagesMock.Object);

        _service = new RecoveryPlanQuotaService(_unitOfWorkMock.Object);
    }

    // Helper to build active subscription with a specific quota
    private UserSubscription MakeActiveSubscription(int limitValue, bool isDeletedQuota = false, bool isActiveQuota = true)
    {
        var quota = new Quota
        {
            Id = Guid.NewGuid(),
            Code = RecoveryPlanQuotaService.QuotaCode,
            Name = "Recovery Plan Request Quota",
            IsDeleted = false,
            IsActive = true
        };

        var planQuota = new SubscriptionPlanQuota
        {
            QuotaId = quota.Id,
            Quota = quota,
            LimitValue = limitValue,
            ResetPeriod = QuotaResetPeriod.SubscriptionCycle,
            IsDeleted = isDeletedQuota,
            IsActive = isActiveQuota
        };

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanQuotas = new List<SubscriptionPlanQuota> { planQuota }
        };

        return new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(25),
            Plan = plan
        };
    }

    // ── ResolveUsageAsync ──────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task ResolveUsageAsync_NoActiveSubscription_ReturnsNoActiveSubscriptionError()
    {
        // Arrange
        _userSubscriptionsMock.Setup(s => s.GetCurrentActiveWithPlanQuotasAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        // Act
        var result = await _service.ResolveUsageAsync(_userId, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NoActiveSubscription));
    }

    [Test]
    [Category("A")]
    public async Task ResolveUsageAsync_QuotaNotConfigured_ReturnsQuotaNotConfiguredError()
    {
        // Arrange
        var subscription = MakeActiveSubscription(10, isDeletedQuota: true); // marked as deleted, won't match active
        _userSubscriptionsMock.Setup(s => s.GetCurrentActiveWithPlanQuotasAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ResolveUsageAsync(_userId, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanQuotaNotConfigured));
    }

    [Test]
    [Category("B")]
    public async Task ResolveUsageAsync_QuotaExhausted_ReturnsQuotaExhaustedError()
    {
        // Arrange
        var subscription = MakeActiveSubscription(0); // Limit is 0
        _userSubscriptionsMock.Setup(s => s.GetCurrentActiveWithPlanQuotasAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ResolveUsageAsync(_userId, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanQuotaExhausted));
    }

    [Test]
    [Category("N")]
    public async Task ResolveUsageAsync_Success_ReturnsUsage()
    {
        // Arrange
        var subscription = MakeActiveSubscription(5);
        var expectedUsage = new UserSubscriptionUsage
        {
            Id = Guid.NewGuid(),
            UserSubscriptionId = subscription.Id,
            QuotaId = subscription.Plan.SubscriptionPlanQuotas.First().QuotaId,
            LimitValue = 5
        };

        _userSubscriptionsMock.Setup(s => s.GetCurrentActiveWithPlanQuotasAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _quotaUsagesMock.Setup(u => u.GetOrCreateAsync(
                subscription.Id,
                subscription.Plan.SubscriptionPlanQuotas.First().QuotaId,
                subscription.StartDate!.Value,
                subscription.EndDate!.Value,
                5,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsage);

        // Act
        var result = await _service.ResolveUsageAsync(_userId, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.SameAs(expectedUsage));
    }

    // ── GetCurrentUsageAsync ───────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetCurrentUsageAsync_NoActiveSubscription_ReturnsNoActiveSubscriptionError()
    {
        // Arrange
        _userSubscriptionsMock.Setup(s => s.GetCurrentActiveWithPlanQuotasAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        // Act
        var result = await _service.GetCurrentUsageAsync(_userId, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NoActiveSubscription));
    }

    [Test]
    [Category("N")]
    public async Task GetCurrentUsageAsync_Success_ReturnsMappedUsages()
    {
        // Arrange
        var subscription = MakeActiveSubscription(10);
        var usages = new List<UserSubscriptionUsage>
        {
            new()
            {
                QuotaId = subscription.Plan.SubscriptionPlanQuotas.First().QuotaId,
                CycleStart = subscription.StartDate!.Value,
                CycleEnd = subscription.EndDate!.Value,
                LimitValue = 10,
                UsedCount = 2,
                ReservedCount = 1
            }
        };

        _userSubscriptionsMock.Setup(s => s.GetCurrentActiveWithPlanQuotasAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _quotaUsagesMock.Setup(u => u.GetBySubscriptionAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usages);

        // Act
        var result = await _service.GetCurrentUsageAsync(_userId, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Has.Count.EqualTo(1));
        var usageResponse = result.Data[0];
        Assert.That(usageResponse.QuotaCode, Is.EqualTo(RecoveryPlanQuotaService.QuotaCode));
        Assert.That(usageResponse.LimitValue, Is.EqualTo(10));
        Assert.That(usageResponse.UsedCount, Is.EqualTo(2));
        Assert.That(usageResponse.ReservedCount, Is.EqualTo(1));
    }

    // ── MutateAsync (Reserve/Release/Consume/Restore) ──────────────────────────

    [Test]
    [Category("A")]
    public async Task MutateAsync_DuplicateIdempotencyKey_ReturnsDuplicate()
    {
        // Arrange
        var key = "idempotency_key";
        var existingLog = new UserSubscriptionLog();

        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLog);

        // Act
        var result = await _service.ReserveAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Duplicate));
    }

    [Test]
    [Category("A")]
    public async Task MutateAsync_MutationReturnsNull_ReturnsRejected()
    {
        // Arrange
        var key = "idempotency_key";
        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);

        _quotaUsagesMock.Setup(u => u.ReserveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuotaMutationResult?)null);

        // Act
        var result = await _service.ReserveAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Rejected));
    }

    [Test]
    [Category("N")]
    public async Task MutateAsync_Success_InsertsLogAndReturnsApplied()
    {
        // Arrange
        var key = "idempotency_key";
        var mutationResult = new QuotaMutationResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            10,
            0,
            0,
            0,
            1
        );

        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);

        _quotaUsagesMock.Setup(u => u.ReserveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mutationResult);

        _quotaUsagesMock.Setup(u => u.TryInsertLogAsync(It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ReserveAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Applied));
        _quotaUsagesMock.Verify(u => u.TryInsertLogAsync(It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task MutateAsync_LogInsertionFails_ReturnsDuplicate()
    {
        // Arrange
        var key = "idempotency_key";
        var mutationResult = new QuotaMutationResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            10,
            0,
            0,
            0,
            0
        );

        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);

        _quotaUsagesMock.Setup(u => u.ReserveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mutationResult);

        _quotaUsagesMock.Setup(u => u.TryInsertLogAsync(It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // insertion fails (concurrent constraint)

        // Act
        var result = await _service.ReserveAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Duplicate));
    }

    // ── ReleaseAsync ─────────────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task ReleaseAsync_Success_CallsRepositoryReleaseAndReturnsApplied()
    {
        var key = "release_key";
        var usageId = Guid.NewGuid();
        var mutationResult = new QuotaMutationResult(
            usageId, Guid.NewGuid(), Guid.NewGuid(), 10, 2, 1, 0, 0);

        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);
        _quotaUsagesMock.Setup(u => u.ReleaseAsync(usageId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mutationResult);
        _quotaUsagesMock.Setup(u => u.TryInsertLogAsync(It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ReleaseAsync(
            usageId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Applied));
        _quotaUsagesMock.Verify(u => u.ReleaseAsync(usageId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _quotaUsagesMock.Verify(u => u.ConsumeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task ReleaseAsync_MutationReturnsNull_ReturnsRejected()
    {
        var key = "release_rejected_key";
        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);
        _quotaUsagesMock.Setup(u => u.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuotaMutationResult?)null);

        var result = await _service.ReleaseAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Rejected));
    }

    // ── ConsumeAsync ─────────────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task ConsumeAsync_Success_CallsRepositoryConsumeAndReturnsApplied()
    {
        var key = "consume_key";
        var usageId = Guid.NewGuid();
        var mutationResult = new QuotaMutationResult(
            usageId, Guid.NewGuid(), Guid.NewGuid(), 10, 3, 0, 1, 1);

        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);
        _quotaUsagesMock.Setup(u => u.ConsumeAsync(usageId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mutationResult);
        _quotaUsagesMock.Setup(u => u.TryInsertLogAsync(It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ConsumeAsync(
            usageId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Applied));
        _quotaUsagesMock.Verify(u => u.ConsumeAsync(usageId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task ConsumeAsync_DuplicateIdempotencyKey_ReturnsDuplicateWithoutCallingRepository()
    {
        var key = "consume_dup_key";
        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscriptionLog());

        var result = await _service.ConsumeAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Duplicate));
        _quotaUsagesMock.Verify(u => u.ConsumeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── RestoreAsync ─────────────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task RestoreAsync_Success_CallsRepositoryRestoreAndReturnsApplied()
    {
        var key = "restore_key";
        var usageId = Guid.NewGuid();
        var mutationResult = new QuotaMutationResult(
            usageId, Guid.NewGuid(), Guid.NewGuid(), 10, 2, 1, 0, 0);

        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);
        _quotaUsagesMock.Setup(u => u.RestoreAsync(usageId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mutationResult);
        _quotaUsagesMock.Setup(u => u.TryInsertLogAsync(It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.RestoreAsync(
            usageId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Applied));
        _quotaUsagesMock.Verify(u => u.RestoreAsync(usageId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("B")]
    public async Task RestoreAsync_LogInsertionFails_ReturnsDuplicate()
    {
        var key = "restore_dup_key";
        var mutationResult = new QuotaMutationResult(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 2, 1, 0, 0);

        _quotaUsagesMock.Setup(u => u.GetLogByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);
        _quotaUsagesMock.Setup(u => u.RestoreAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mutationResult);
        _quotaUsagesMock.Setup(u => u.TryInsertLogAsync(It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.RestoreAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            key, DateTime.UtcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Duplicate));
    }
}

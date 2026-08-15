using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Requests;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.SubscriptionPlanQuotas;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class SubscriptionPlanQuotaServiceTests
{
    private Mock<ISubscriptionPlanQuotaRepository> _repositoryMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<ISubscriptionPlanCacheInvalidator> _cacheInvalidatorMock = null!;
    private Mock<ILogger<SubscriptionPlanQuotaService>> _loggerMock = null!;
    private Mock<IGenericRepository<SubscriptionPlan>> _subscriptionPlansMock = null!;
    private SubscriptionPlanQuotaService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<ISubscriptionPlanQuotaRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheInvalidatorMock = new Mock<ISubscriptionPlanCacheInvalidator>();
        _loggerMock = new Mock<ILogger<SubscriptionPlanQuotaService>>();
        _subscriptionPlansMock = new Mock<IGenericRepository<SubscriptionPlan>>();

        _unitOfWorkMock.Setup(u => u.SubscriptionPlans).Returns(_subscriptionPlansMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _service = new SubscriptionPlanQuotaService(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheInvalidatorMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public async Task ListQuotaDefinitionsAsync_Success_ReturnsSortedResponse()
    {
        var quotaB = MakeQuota(code: "B_QUOTA");
        var quotaA = MakeQuota(code: "a_quota");
        _repositoryMock.Setup(repository => repository.ListQuotaDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { quotaB, quotaA });

        var result = await _service.ListQuotaDefinitionsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data![0].Code, Is.EqualTo("a_quota"));
            Assert.That(result.Data[1].Code, Is.EqualTo("B_QUOTA"));
        });
    }

    [Test]
    public async Task ListQuotaDefinitionsAsync_RepositoryThrows_ReturnsConflictFailure()
    {
        _repositoryMock.Setup(repository => repository.ListQuotaDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db error"));

        var result = await _service.ListQuotaDefinitionsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaConflict));
        });
    }

    [Test]
    public void ListQuotaDefinitionsAsync_CancellationRequested_Rethrows()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        _repositoryMock.Setup(repository => repository.ListQuotaDefinitionsAsync(cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));

        Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.ListQuotaDefinitionsAsync(cancellationSource.Token));
    }

    [Test]
    public async Task ListPlanQuotasAsync_EmptyPlanId_ReturnsInvalidRequest()
    {
        var result = await _service.ListPlanQuotasAsync(Guid.Empty, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.InvalidRequest));
        });
    }

    [Test]
    public async Task ListPlanQuotasAsync_PlanNotFound_ReturnsSubscriptionPlanNotFound()
    {
        var planId = Guid.NewGuid();
        SetupPlanLookup(planId, null);

        var result = await _service.ListPlanQuotasAsync(planId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.SubscriptionPlanNotFound));
        });
    }

    [Test]
    public async Task ListPlanQuotasAsync_Success_ReturnsSortedMappings()
    {
        var planId = Guid.NewGuid();
        SetupPlanLookup(planId, new SubscriptionPlan { Id = planId });
        var mappingB = MakeMapping(planId, quota: MakeQuota(code: "Z"));
        var mappingA = MakeMapping(planId, quota: MakeQuota(code: "A"));
        _repositoryMock.Setup(repository => repository.ListPlanQuotasAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mappingB, mappingA });

        var result = await _service.ListPlanQuotasAsync(planId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data![0].QuotaCode, Is.EqualTo("A"));
            Assert.That(result.Data[1].QuotaCode, Is.EqualTo("Z"));
        });
    }

    [Test]
    public async Task ListPlanQuotasAsync_RepositoryThrows_ReturnsConflictFailure()
    {
        var planId = Guid.NewGuid();
        SetupPlanLookup(planId, new SubscriptionPlan { Id = planId });
        _repositoryMock.Setup(repository => repository.ListPlanQuotasAsync(planId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _service.ListPlanQuotasAsync(planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaConflict));
    }

    [TestCase(-1, QuotaResetPeriod.SubscriptionCycle)]
    [TestCase(0, (QuotaResetPeriod)999)]
    public async Task UpsertPlanQuotaAsync_InvalidRequest_ReturnsInvalidRequestWithoutTransaction(
        int limitValue, QuotaResetPeriod resetPeriod)
    {
        var request = new UpsertSubscriptionPlanQuotaRequest { LimitValue = limitValue, ResetPeriod = resetPeriod };

        var result = await _service.UpsertPlanQuotaAsync(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.InvalidRequest));
        });
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task UpsertPlanQuotaAsync_NullRequest_ReturnsInvalidRequest()
    {
        var result = await _service.UpsertPlanQuotaAsync(Guid.NewGuid(), Guid.NewGuid(), null!, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.InvalidRequest));
    }

    [Test]
    public async Task UpsertPlanQuotaAsync_PlanNotFound_RollsBackAndReturnsNotFound()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await _service.UpsertPlanQuotaAsync(planId, quotaId, MakeValidRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.SubscriptionPlanNotFound));
        });
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task UpsertPlanQuotaAsync_QuotaNotFound_RollsBackAndReturnsQuotaNotFound()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = planId });
        _repositoryMock.Setup(repository => repository.GetQuotaDefinitionAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quota?)null);

        var result = await _service.UpsertPlanQuotaAsync(planId, quotaId, MakeValidRequest(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.QuotaNotFound));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task UpsertPlanQuotaAsync_QuotaInactive_RollsBackAndReturnsQuotaInactive()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = planId });
        _repositoryMock.Setup(repository => repository.GetQuotaDefinitionAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuota(isActive: false));

        var result = await _service.UpsertPlanQuotaAsync(planId, quotaId, MakeValidRequest(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.QuotaInactive));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task UpsertPlanQuotaAsync_NoExistingMapping_CreatesNewMappingAndInvalidatesCache()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        SetupUpsertPrereqs(planId, quotaId);
        _repositoryMock.Setup(repository => repository.GetNonDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanQuota?)null);
        _repositoryMock.Setup(repository => repository.GetLatestDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanQuota?)null);

        var result = await _service.UpsertPlanQuotaAsync(planId, quotaId, MakeValidRequest(limitValue: 42), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data!.LimitValue, Is.EqualTo(42));
        });
        _repositoryMock.Verify(repository => repository.Add(It.Is<SubscriptionPlanQuota>(m => m.PlanId == planId && m.QuotaId == quotaId && m.LimitValue == 42)), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(invalidator => invalidator.InvalidateAsync(planId, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task UpsertPlanQuotaAsync_ResurrectsLatestDeletedMapping()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        SetupUpsertPrereqs(planId, quotaId);
        var deletedMapping = MakeMapping(planId, quotaId: quotaId);
        deletedMapping.IsDeleted = true;
        _repositoryMock.Setup(repository => repository.GetNonDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanQuota?)null);
        _repositoryMock.Setup(repository => repository.GetLatestDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedMapping);

        var result = await _service.UpsertPlanQuotaAsync(planId, quotaId, MakeValidRequest(limitValue: 7), CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(deletedMapping.IsDeleted, Is.False);
            Assert.That(deletedMapping.DeletedAt, Is.Null);
            Assert.That(deletedMapping.LimitValue, Is.EqualTo(7));
        });
        _repositoryMock.Verify(repository => repository.Add(It.IsAny<SubscriptionPlanQuota>()), Times.Never);
    }

    [Test]
    public async Task UpsertPlanQuotaAsync_UnchangedMapping_SkipsUpdatingTimestampButStillCommits()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        SetupUpsertPrereqs(planId, quotaId);
        var existingMapping = MakeMapping(planId, quotaId: quotaId, limitValue: 10, isActive: true);
        existingMapping.ResetPeriod = QuotaResetPeriod.SubscriptionCycle;
        existingMapping.UpdatedAt = null;
        _repositoryMock.Setup(repository => repository.GetNonDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMapping);

        var request = new UpsertSubscriptionPlanQuotaRequest
        {
            LimitValue = 10,
            ResetPeriod = QuotaResetPeriod.SubscriptionCycle,
            IsActive = true,
        };

        var result = await _service.UpsertPlanQuotaAsync(planId, quotaId, request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(existingMapping.UpdatedAt, Is.Null);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpsertPlanQuotaAsync_SaveChangesThrows_RollsBackAndReturnsConflict()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        SetupUpsertPrereqs(planId, quotaId);
        _repositoryMock.Setup(repository => repository.GetNonDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanQuota?)null);
        _repositoryMock.Setup(repository => repository.GetLatestDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanQuota?)null);
        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));

        var result = await _service.UpsertPlanQuotaAsync(planId, quotaId, MakeValidRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaConflict));
        });
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _cacheInvalidatorMock.Verify(invalidator => invalidator.InvalidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void UpsertPlanQuotaAsync_CancellationRequested_RollsBackAndRethrows()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        using var cancellationSource = new CancellationTokenSource();
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            });

        Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.UpsertPlanQuotaAsync(planId, quotaId, MakeValidRequest(), cancellationSource.Token));

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task DeletePlanQuotaAsync_EmptyIds_ReturnsInvalidRequest()
    {
        var result = await _service.DeletePlanQuotaAsync(Guid.Empty, Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.InvalidRequest));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DeletePlanQuotaAsync_PlanNotFound_RollsBackAndReturnsNotFound()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await _service.DeletePlanQuotaAsync(planId, quotaId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.SubscriptionPlanNotFound));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task DeletePlanQuotaAsync_MappingNotFound_RollsBackAndReturnsNotFound()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = planId });
        _repositoryMock.Setup(repository => repository.GetNonDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanQuota?)null);

        var result = await _service.DeletePlanQuotaAsync(planId, quotaId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaNotFound));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task DeletePlanQuotaAsync_ValidMapping_SoftDeletesCommitsAndInvalidatesCache()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        var mapping = MakeMapping(planId, quotaId: quotaId);
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = planId });
        _repositoryMock.Setup(repository => repository.GetNonDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mapping);

        var result = await _service.DeletePlanQuotaAsync(planId, quotaId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.True);
            Assert.That(mapping.IsDeleted, Is.True);
            Assert.That(mapping.DeletedAt, Is.Not.Null);
        });
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(invalidator => invalidator.InvalidateAsync(planId, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task DeletePlanQuotaAsync_SaveChangesThrows_RollsBackAndReturnsConflict()
    {
        var planId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        var mapping = MakeMapping(planId, quotaId: quotaId);
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = planId });
        _repositoryMock.Setup(repository => repository.GetNonDeletedMappingAsync(planId, quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mapping);
        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));

        var result = await _service.DeletePlanQuotaAsync(planId, quotaId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaConflict));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task GetActivePlanQuotasAsync_EmptyPlanIds_ReturnsEmptyDictionaryWithoutCallingRepository()
    {
        var result = await _service.GetActivePlanQuotasAsync(Array.Empty<Guid>(), CancellationToken.None);

        Assert.That(result, Is.Empty);
        _repositoryMock.Verify(repository => repository.ListActivePlanQuotasAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetActivePlanQuotasAsync_MissingPlanGetsEmptyList()
    {
        var planWithQuotas = Guid.NewGuid();
        var planWithoutQuotas = Guid.NewGuid();
        var mapping = MakeMapping(planWithQuotas, quota: MakeQuota(code: "X"));
        _repositoryMock.Setup(repository => repository.ListActivePlanQuotasAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mapping });

        var result = await _service.GetActivePlanQuotasAsync(new[] { planWithQuotas, planWithoutQuotas }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result[planWithQuotas], Has.Count.EqualTo(1));
            Assert.That(result[planWithoutQuotas], Is.Empty);
        });
    }

    private void SetupPlanLookup(Guid planId, SubscriptionPlan? plan)
    {
        _subscriptionPlansMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
    }

    private void SetupUpsertPrereqs(Guid planId, Guid quotaId)
    {
        _repositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { Id = planId });
        _repositoryMock.Setup(repository => repository.GetQuotaDefinitionAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuota(isActive: true));
    }

    private static UpsertSubscriptionPlanQuotaRequest MakeValidRequest(int limitValue = 1) =>
        new()
        {
            LimitValue = limitValue,
            ResetPeriod = QuotaResetPeriod.SubscriptionCycle,
            IsActive = true,
        };

    private static Quota MakeQuota(string code = "QUOTA", bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"{code} name",
            Unit = "count",
            IsActive = isActive,
        };

    private static SubscriptionPlanQuota MakeMapping(
        Guid planId,
        Guid? quotaId = null,
        Quota? quota = null,
        int limitValue = 5,
        bool isActive = true)
    {
        var resolvedQuota = quota ?? MakeQuota();
        return new SubscriptionPlanQuota
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            QuotaId = quotaId ?? resolvedQuota.Id,
            LimitValue = limitValue,
            ResetPeriod = QuotaResetPeriod.SubscriptionCycle,
            IsActive = isActive,
            Quota = resolvedQuota,
        };
    }
}

using System.Linq.Expressions;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.ServiceCredits;
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
public class LabTestQuotaServiceTests
{
    private Mock<IServiceCreditService> _serviceCreditServiceMock = null!;
    private Mock<IGenericRepository<LabTestSession>> _sessionsMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IQuotaUsageRepository> _quotaUsageRepositoryMock = null!;
    private Mock<ILogger<LabTestQuotaService>> _loggerMock = null!;
    private LabTestQuotaService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceCreditServiceMock = new Mock<IServiceCreditService>();
        _sessionsMock = new Mock<IGenericRepository<LabTestSession>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _quotaUsageRepositoryMock = new Mock<IQuotaUsageRepository>();
        _loggerMock = new Mock<ILogger<LabTestQuotaService>>();

        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.QuotaUsages).Returns(_quotaUsageRepositoryMock.Object);

        _service = new LabTestQuotaService(
            _serviceCreditServiceMock.Object,
            _sessionsMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public async Task ReserveAsync_DelegatesToServiceCreditServiceWithLabTestReferenceAndIdempotencyKey()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc);
        var usage = new UserSubscriptionUsage { Id = Guid.NewGuid() };

        _serviceCreditServiceMock.Setup(service => service.ReserveAsync(
                userId,
                "LabTestSession",
                sessionId,
                actorUserId,
                $"labtest:reserve:{sessionId:N}",
                "Lab test session service credit reserved.",
                utcNow,
                CancellationToken.None))
            .ReturnsAsync(ServiceCreditOperationResult<UserSubscriptionUsage>.Ok(usage));

        var result = await _service.ReserveAsync(userId, sessionId, actorUserId, utcNow, CancellationToken.None);

        Assert.That(result.Data, Is.SameAs(usage));
        _serviceCreditServiceMock.VerifyAll();
    }

    [Test]
    public async Task FinalizeAsync_SessionNotFound_ReturnsWithoutStartingTransaction()
    {
        SetupSession(null);

        await _service.FinalizeAsync(Guid.NewGuid(), CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FinalizeAsync_NoFinalizeActionForProcessingStatus_ReturnsWithoutStartingTransaction()
    {
        var session = MakeSession(status: LabTestSessionStatus.Processing);
        SetupSession(session);

        await _service.FinalizeAsync(session.Id, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FinalizeAsync_CompletedSession_ConsumesUsageWithLabTestIdempotencyKey()
    {
        var session = MakeSession(status: LabTestSessionStatus.Completed);
        var usage = new UserSubscriptionUsage
        {
            Id = session.UserSubscriptionUsageId!.Value,
            UserSubscriptionId = session.UserSubscriptionId!.Value,
            QuotaId = Guid.NewGuid()
        };
        SetupSession(session);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetByIdAsync(usage.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);
        _serviceCreditServiceMock.Setup(service => service.MutateAsync(
                usage.Id,
                usage.UserSubscriptionId,
                usage.QuotaId,
                SubscriptionQuotaActionType.Consume,
                "LabTestSession",
                session.Id,
                session.UserId,
                $"labtest:consume:{session.Id:N}",
                "Lab test session service credit consumed.",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Applied);

        await _service.FinalizeAsync(session.Id, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _serviceCreditServiceMock.VerifyAll();
    }

    [Test]
    public void FinalizeAsync_UsageSubscriptionMismatch_RollsBackAndThrows()
    {
        var session = MakeSession(status: LabTestSessionStatus.Failed);
        var usage = new UserSubscriptionUsage
        {
            Id = session.UserSubscriptionUsageId!.Value,
            UserSubscriptionId = Guid.NewGuid(),
            QuotaId = Guid.NewGuid()
        };
        SetupSession(session);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetByIdAsync(usage.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.FinalizeAsync(session.Id, CancellationToken.None));

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupSession(LabTestSession? session)
    {
        _sessionsMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabTestSession, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private static LabTestSession MakeSession(
        LabTestSessionStatus status = LabTestSessionStatus.Completed) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserSubscriptionId = Guid.NewGuid(),
            UserSubscriptionUsageId = Guid.NewGuid(),
            Status = status
        };
}

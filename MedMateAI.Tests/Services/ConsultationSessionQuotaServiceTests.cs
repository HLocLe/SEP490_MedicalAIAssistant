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
public class ConsultationSessionQuotaServiceTests
{
    private Mock<IServiceCreditService> _serviceCreditServiceMock = null!;
    private Mock<IGenericRepository<ConsultationSession>> _sessionsMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IQuotaUsageRepository> _quotaUsageRepositoryMock = null!;
    private Mock<ILogger<ConsultationSessionQuotaService>> _loggerMock = null!;
    private ConsultationSessionQuotaService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceCreditServiceMock = new Mock<IServiceCreditService>();
        _sessionsMock = new Mock<IGenericRepository<ConsultationSession>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _quotaUsageRepositoryMock = new Mock<IQuotaUsageRepository>();
        _loggerMock = new Mock<ILogger<ConsultationSessionQuotaService>>();

        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.QuotaUsages).Returns(_quotaUsageRepositoryMock.Object);

        _service = new ConsultationSessionQuotaService(
            _serviceCreditServiceMock.Object,
            _sessionsMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public async Task ReserveAsync_DelegatesToServiceCreditServiceWithExpectedParameters()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc);
        var usage = new UserSubscriptionUsage { Id = Guid.NewGuid() };

        _serviceCreditServiceMock.Setup(service => service.ReserveAsync(
                userId,
                "ConsultationSession",
                sessionId,
                actorUserId,
                $"consultation-session:reserve:{sessionId:N}",
                "Consultation session service credit reserved.",
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
    public async Task FinalizeAsync_NoSubscriptionLinkage_ReturnsWithoutStartingTransaction()
    {
        var session = MakeSession(subscriptionId: null, usageId: null, status: ConsultationSessionStatus.Completed);
        SetupSession(session);

        await _service.FinalizeAsync(session.Id, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FinalizeAsync_InconsistentLinkage_LogsErrorAndReturnsWithoutStartingTransaction()
    {
        var session = MakeSession(subscriptionId: Guid.NewGuid(), usageId: null, status: ConsultationSessionStatus.Completed);
        SetupSession(session);

        await _service.FinalizeAsync(session.Id, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FinalizeAsync_NoFinalizeActionForStatus_ReturnsWithoutStartingTransaction()
    {
        var session = MakeSession(status: ConsultationSessionStatus.Processing);
        SetupSession(session);

        await _service.FinalizeAsync(session.Id, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FinalizeAsync_CompletedSession_ConsumesUsageAndCommits()
    {
        var session = MakeSession(status: ConsultationSessionStatus.Completed);
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
                "ConsultationSession",
                session.Id,
                session.UserId,
                $"consultation-session:consume:{session.Id:N}",
                "Consultation session service credit consumed.",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Applied);

        await _service.FinalizeAsync(session.Id, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FinalizeAsync_FailedSession_ReleasesUsageAndCommits()
    {
        var session = MakeSession(status: ConsultationSessionStatus.Failed);
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
                SubscriptionQuotaActionType.Release,
                "ConsultationSession",
                session.Id,
                session.UserId,
                $"consultation-session:release:{session.Id:N}",
                "Consultation session service credit released.",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Applied);

        await _service.FinalizeAsync(session.Id, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void FinalizeAsync_UsageMissingOrMismatched_RollsBackAndThrows()
    {
        var session = MakeSession(status: ConsultationSessionStatus.Completed);
        SetupSession(session);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetByIdAsync(
                session.UserSubscriptionUsageId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionUsage?)null);

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.FinalizeAsync(session.Id, CancellationToken.None));

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void FinalizeAsync_MutationRejected_RollsBackAndThrows()
    {
        var session = MakeSession(status: ConsultationSessionStatus.Completed);
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
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<SubscriptionQuotaActionType>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Rejected);

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.FinalizeAsync(session.Id, CancellationToken.None));

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    private void SetupSession(ConsultationSession? session)
    {
        _sessionsMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<ConsultationSession, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private static ConsultationSession MakeSession(
        ConsultationSessionStatus status = ConsultationSessionStatus.Completed) =>
        MakeSession(Guid.NewGuid(), Guid.NewGuid(), status);

    private static ConsultationSession MakeSession(
        Guid? subscriptionId,
        Guid? usageId,
        ConsultationSessionStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserSubscriptionId = subscriptionId,
            UserSubscriptionUsageId = usageId,
            Status = status
        };
}

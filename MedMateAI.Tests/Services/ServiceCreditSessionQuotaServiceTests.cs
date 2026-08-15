using System.Linq.Expressions;
using MedMateAI.Application.IService;
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
public class ServiceCreditSessionQuotaServiceTests
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
    public void FinalizeAsync_ServiceCreditServiceThrows_RollsBackWithNoneTokenAndRethrows()
    {
        using var cancellationSource = new CancellationTokenSource();
        var session = MakeSession(ConsultationSessionStatus.Completed);
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
            .ThrowsAsync(new TimeoutException("downstream timeout"));

        Assert.ThrowsAsync<TimeoutException>(
            () => _service.FinalizeAsync(session.Id, cancellationSource.Token));

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FinalizeAsync_Success_BeginsTransactionWithCallerTokenBeforeMutating()
    {
        using var cancellationSource = new CancellationTokenSource();
        var session = MakeSession(ConsultationSessionStatus.Completed);
        var usage = new UserSubscriptionUsage
        {
            Id = session.UserSubscriptionUsageId!.Value,
            UserSubscriptionId = session.UserSubscriptionId!.Value,
            QuotaId = Guid.NewGuid()
        };
        var callOrder = new List<string>();
        SetupSession(session);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetByIdAsync(usage.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);
        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.BeginTransactionAsync(cancellationSource.Token))
            .Callback(() => callOrder.Add("begin"))
            .Returns(Task.CompletedTask);
        _serviceCreditServiceMock.Setup(service => service.MutateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<SubscriptionQuotaActionType>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("mutate"))
            .ReturnsAsync(MedMateAI.Application.Models.QuotaMutationStatus.Applied);
        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.CommitTransactionAsync(cancellationSource.Token))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);

        await _service.FinalizeAsync(session.Id, cancellationSource.Token);

        Assert.That(callOrder, Is.EqualTo(new[] { "begin", "mutate", "commit" }));
    }

    private void SetupSession(ConsultationSession session)
    {
        _sessionsMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<ConsultationSession, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private static ConsultationSession MakeSession(ConsultationSessionStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserSubscriptionId = Guid.NewGuid(),
            UserSubscriptionUsageId = Guid.NewGuid(),
            Status = status
        };
}

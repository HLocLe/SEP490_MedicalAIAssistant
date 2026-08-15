using MedMateAI.Application.IService;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Application.Options;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanAssignmentTimeoutProcessorTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IRecoveryPlanRequestRepository> _requestRepositoryMock = null!;
    private Mock<IRecoveryPlanRealtimeNotifier> _realtimeNotifierMock = null!;
    private Mock<ILogger<RecoveryPlanAssignmentTimeoutProcessor>> _loggerMock = null!;
    private RecoveryPlanAssignmentTimeoutProcessor _processor = null!;

    private readonly DateTime _utcNow = new(2026, 8, 13, 8, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _requestRepositoryMock = new Mock<IRecoveryPlanRequestRepository>();
        _realtimeNotifierMock = new Mock<IRecoveryPlanRealtimeNotifier>();
        _loggerMock = new Mock<ILogger<RecoveryPlanAssignmentTimeoutProcessor>>();

        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.RecoveryPlanRequests).Returns(_requestRepositoryMock.Object);

        var options = Options.Create(new RecoveryPlanJobOptions { LifecycleBatchSize = 20 });

        _processor = new RecoveryPlanAssignmentTimeoutProcessor(
            _unitOfWorkMock.Object,
            _realtimeNotifierMock.Object,
            options,
            _loggerMock.Object);
    }

    [Test]
    public async Task ProcessBatchAsync_NoExpiredAssignments_DoesNothing()
    {
        _requestRepositoryMock.Setup(repository => repository.GetExpiredAssignmentIdsAsync(
                _utcNow, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _processor.ProcessBatchAsync(_utcNow, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessBatchAsync_RequestNoLongerExpired_RollsBackWithoutNotifying()
    {
        var requestId = Guid.NewGuid();
        _requestRepositoryMock.Setup(repository => repository.GetExpiredAssignmentIdsAsync(
                _utcNow, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { requestId });
        _requestRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlanRequest?)null);

        await _processor.ProcessBatchAsync(_utcNow, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtimeNotifierMock.Verify(notifier => notifier.TryNotifyRequestChangedAsync(
            It.IsAny<RecoveryPlanRequestRealtimeNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.ClearTrackedChanges(), Times.Once);
    }

    [Test]
    public async Task ProcessBatchAsync_ExpiredAssignment_ReopensRequestCommitsAndNotifies()
    {
        var requestId = Guid.NewGuid();
        var previousDoctorId = Guid.NewGuid();
        var request = MakeExpiredRequest(requestId, previousDoctorId);

        _requestRepositoryMock.Setup(repository => repository.GetExpiredAssignmentIdsAsync(
                _utcNow, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { requestId });
        _requestRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await _processor.ProcessBatchAsync(_utcNow, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(request.Status, Is.EqualTo(RecoveryPlanRequestStatus.WaitingForDoctor));
            Assert.That(request.AssignedDoctorId, Is.Null);
            Assert.That(request.AcceptedAt, Is.Null);
            Assert.That(request.ReviewStartedAt, Is.Null);
            Assert.That(request.AssignmentExpiresAt, Is.Null);
            Assert.That(request.Version, Is.EqualTo(1));
        });
        _requestRepositoryMock.Verify(repository => repository.AddEvent(
            It.Is<RecoveryPlanRequestEvent>(evt =>
                evt.RecoveryPlanRequestId == requestId
                && evt.EventType == RecoveryPlanRequestEventType.Reopened
                && evt.FromStatus == RecoveryPlanRequestStatus.Assigned
                && evt.ToStatus == RecoveryPlanRequestStatus.WaitingForDoctor)), Times.Once);
        _requestRepositoryMock.Verify(repository => repository.AddOutbox(It.IsAny<OutboxMessage>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtimeNotifierMock.Verify(notifier => notifier.TryNotifyRequestChangedAsync(
            It.Is<RecoveryPlanRequestRealtimeNotification>(n =>
                n.RequestId == requestId && n.TargetDoctorId == previousDoctorId),
            CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.ClearTrackedChanges(), Times.Once);
    }

    [Test]
    public async Task ProcessBatchAsync_SaveChangesThrows_RollsBackLogsWarningAndContinuesBatch()
    {
        var firstRequestId = Guid.NewGuid();
        var secondRequestId = Guid.NewGuid();
        var firstRequest = MakeExpiredRequest(firstRequestId, Guid.NewGuid());
        var secondRequest = MakeExpiredRequest(secondRequestId, Guid.NewGuid());

        _requestRepositoryMock.Setup(repository => repository.GetExpiredAssignmentIdsAsync(
                _utcNow, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { firstRequestId, secondRequestId });
        _requestRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(firstRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstRequest);
        _requestRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(secondRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondRequest);
        _unitOfWorkMock.SetupSequence(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"))
            .ReturnsAsync(1);

        await _processor.ProcessBatchAsync(_utcNow, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.ClearTrackedChanges(), Times.Exactly(2));
    }

    [Test]
    public void ProcessBatchAsync_CancellationRequested_RollsBackAndRethrows()
    {
        var requestId = Guid.NewGuid();
        using var cancellationSource = new CancellationTokenSource();

        _requestRepositoryMock.Setup(repository => repository.GetExpiredAssignmentIdsAsync(
                _utcNow, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { requestId });
        _requestRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(requestId, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            });

        Assert.ThrowsAsync<OperationCanceledException>(
            () => _processor.ProcessBatchAsync(_utcNow, cancellationSource.Token));

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    private static RecoveryPlanRequest MakeExpiredRequest(Guid id, Guid assignedDoctorId) =>
        new()
        {
            Id = id,
            UserId = Guid.NewGuid(),
            Status = RecoveryPlanRequestStatus.Assigned,
            AssignedDoctorId = assignedDoctorId,
            AssignmentExpiresAt = new DateTime(2026, 8, 13, 7, 0, 0, DateTimeKind.Utc),
            IsDeleted = false,
            Version = 0,
            RequestedAt = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)
        };
}

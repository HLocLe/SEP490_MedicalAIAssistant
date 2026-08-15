using MedMateAI.Application.IService;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Application.Options;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
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
public class RecoveryPlanCompletionProcessorTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IRecoveryPlanRepository> _recoveryPlanRepositoryMock = null!;
    private Mock<IRecoveryPlanRealtimeNotifier> _realtimeNotifierMock = null!;
    private Mock<ILogger<RecoveryPlanCompletionProcessor>> _loggerMock = null!;
    private RecoveryPlanCompletionProcessor _processor = null!;

    private readonly DateTime _utcNowDue = new(2026, 8, 13, 20, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _utcNowNotDue = new(2026, 8, 13, 8, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _recoveryPlanRepositoryMock = new Mock<IRecoveryPlanRepository>();
        _realtimeNotifierMock = new Mock<IRecoveryPlanRealtimeNotifier>();
        _loggerMock = new Mock<ILogger<RecoveryPlanCompletionProcessor>>();

        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.RecoveryPlans).Returns(_recoveryPlanRepositoryMock.Object);

        _processor = new RecoveryPlanCompletionProcessor(
            _unitOfWorkMock.Object,
            _realtimeNotifierMock.Object,
            Options.Create(new RecoveryPlanJobOptions { LifecycleBatchSize = 2 }),
            _loggerMock.Object);
    }

    [Test]
    public async Task ProcessBatchAsync_NoCandidateDueYet_DoesNothing()
    {
        var candidate = new RecoveryPlanCompletionCandidate(
            Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(_utcNowNotDue), "Asia/Ho_Chi_Minh");
        SetupCandidatePage(new[] { candidate });

        await _processor.ProcessBatchAsync(_utcNowNotDue, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessBatchAsync_DueCandidateWithActivePlan_CompletesPlanCommitsAndNotifies()
    {
        var planId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var endDate = DateOnly.FromDateTime(_utcNowNotDue);
        var candidate = new RecoveryPlanCompletionCandidate(planId, userId, endDate, "Asia/Ho_Chi_Minh");
        var plan = MakeActivePlan(planId, userId, endDate);
        SetupCandidatePage(new[] { candidate });
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetUserTimeZoneIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Asia/Ho_Chi_Minh");

        await _processor.ProcessBatchAsync(_utcNowDue, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Status, Is.EqualTo(RecoveryPlanStatus.Completed));
            Assert.That(plan.CompletedAt, Is.EqualTo(_utcNowDue));
            Assert.That(plan.IsCurrent, Is.False);
        });
        _recoveryPlanRepositoryMock.Verify(repository => repository.AddOutbox(It.Is<OutboxMessage>(message =>
            message.AggregateId == planId
            && message.EventType == "RecoveryPlanCompleted")), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtimeNotifierMock.Verify(notifier => notifier.TryNotifyPlanChangedAsync(
            It.Is<RecoveryPlanLifecycleRealtimeNotification>(n => n.PlanId == planId && n.EventType == "RecoveryPlanCompleted"),
            CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task ProcessBatchAsync_PlanNoLongerActive_RollsBackWithoutCompleting()
    {
        var planId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var endDate = DateOnly.FromDateTime(_utcNowNotDue);
        var candidate = new RecoveryPlanCompletionCandidate(planId, userId, endDate, "Asia/Ho_Chi_Minh");
        var plan = MakeActivePlan(planId, userId, endDate);
        plan.Status = RecoveryPlanStatus.Cancelled;
        SetupCandidatePage(new[] { candidate });
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        await _processor.ProcessBatchAsync(_utcNowDue, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _recoveryPlanRepositoryMock.Verify(repository => repository.GetUserTimeZoneIdAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessBatchAsync_RecheckAtPlanLevelNotDue_RollsBackWithoutCompleting()
    {
        var planId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var endDate = DateOnly.FromDateTime(_utcNowNotDue);
        var candidate = new RecoveryPlanCompletionCandidate(planId, userId, endDate, "Asia/Ho_Chi_Minh");
        var plan = MakeActivePlan(planId, userId, endDate);
        SetupCandidatePage(new[] { candidate });
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetUserTimeZoneIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Etc/GMT+12");

        await _processor.ProcessBatchAsync(_utcNowDue, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessBatchAsync_InvalidUserTimeZone_FallsBackToDefaultAndLogsWarning()
    {
        var planId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var endDate = DateOnly.FromDateTime(_utcNowNotDue);
        var candidate = new RecoveryPlanCompletionCandidate(planId, userId, endDate, "Asia/Ho_Chi_Minh");
        var plan = MakeActivePlan(planId, userId, endDate);
        SetupCandidatePage(new[] { candidate });
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetUserTimeZoneIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Not/A/Real/Zone");

        await _processor.ProcessBatchAsync(_utcNowDue, CancellationToken.None);

        Assert.That(plan.Status, Is.EqualTo(RecoveryPlanStatus.Completed));
        _loggerMock.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, type) => state.ToString()!.Contains("default timezone")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Test]
    public async Task ProcessBatchAsync_SaveChangesThrows_RollsBackLogsWarningAndClearsTrackedChanges()
    {
        var planId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var endDate = DateOnly.FromDateTime(_utcNowNotDue);
        var candidate = new RecoveryPlanCompletionCandidate(planId, userId, endDate, "Asia/Ho_Chi_Minh");
        var plan = MakeActivePlan(planId, userId, endDate);
        SetupCandidatePage(new[] { candidate });
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetUserTimeZoneIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Asia/Ho_Chi_Minh");
        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));

        await _processor.ProcessBatchAsync(_utcNowDue, CancellationToken.None);

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.ClearTrackedChanges(), Times.Once);
        _realtimeNotifierMock.Verify(notifier => notifier.TryNotifyPlanChangedAsync(
            It.IsAny<RecoveryPlanLifecycleRealtimeNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void ProcessBatchAsync_CancellationRequested_RollsBackAndRethrows()
    {
        var planId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var endDate = DateOnly.FromDateTime(_utcNowNotDue);
        var candidate = new RecoveryPlanCompletionCandidate(planId, userId, endDate, "Asia/Ho_Chi_Minh");
        using var cancellationSource = new CancellationTokenSource();
        SetupCandidatePage(new[] { candidate });
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(planId, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            });

        Assert.ThrowsAsync<OperationCanceledException>(
            () => _processor.ProcessBatchAsync(_utcNowDue, cancellationSource.Token));

        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    private void SetupCandidatePage(IReadOnlyList<RecoveryPlanCompletionCandidate> candidates)
    {
        _recoveryPlanRepositoryMock.Setup(repository => repository.GetActiveCompletionCandidatesAsync(
                It.IsAny<DateOnly>(), 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
    }

    private static RecoveryPlan MakeActivePlan(Guid planId, Guid userId, DateOnly endDate) =>
        new()
        {
            Id = planId,
            UserId = userId,
            DoctorId = Guid.NewGuid(),
            RecoveryPlanRequestId = Guid.NewGuid(),
            Status = RecoveryPlanStatus.Active,
            EndDate = endDate,
            IsCurrent = true,
            IsDeleted = false
        };
}

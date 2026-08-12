using MedMateAI.Application.Common;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.RecoveryPlans;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IRecoveryPlanRepository> _planRepoMock = null!;
    private Mock<IDoctorRepository> _doctorRepoMock = null!;
    private Mock<IRecoveryPlanRequestRepository> _requestRepoMock = null!;
    private Mock<IQuotaUsageRepository> _quotaUsageRepoMock = null!;
    private Mock<IRecoveryPlanQuotaService> _quotaMock = null!;
    private Mock<IRecoveryPlanClinicalContextService> _clinicalContextMock = null!;
    private Mock<IRecoveryPlanRealtimeNotifier> _realtimeNotifierMock = null!;
    private RecoveryPlanService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _doctorUserId = Guid.NewGuid();
    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();
    private readonly Guid _usageId = Guid.NewGuid();
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Guid _quotaId = Guid.NewGuid();

    private const string SerializedSnapshot = "{\"schemaVersion\":1}";

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _planRepoMock = new Mock<IRecoveryPlanRepository>();
        _doctorRepoMock = new Mock<IDoctorRepository>();
        _requestRepoMock = new Mock<IRecoveryPlanRequestRepository>();
        _quotaUsageRepoMock = new Mock<IQuotaUsageRepository>();
        _quotaMock = new Mock<IRecoveryPlanQuotaService>();
        _clinicalContextMock = new Mock<IRecoveryPlanClinicalContextService>();
        _realtimeNotifierMock = new Mock<IRecoveryPlanRealtimeNotifier>();

        _unitOfWorkMock.Setup(u => u.RecoveryPlans).Returns(_planRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Doctors).Returns(_doctorRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.RecoveryPlanRequests).Returns(_requestRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.QuotaUsages).Returns(_quotaUsageRepoMock.Object);

        // Transaction setups
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _requestRepoMock.Setup(repository => repository.LockUserRecoveryPlanWorkflowAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _realtimeNotifierMock.Setup(n => n.TryNotifyPlanChangedAsync(
                It.IsAny<RecoveryPlanLifecycleRealtimeNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _clinicalContextMock.Setup(c => c.SerializeSnapshot(It.IsAny<RecoveryPlanClinicalSnapshot>()))
            .Returns(SerializedSnapshot);

        _service = new RecoveryPlanService(
            _unitOfWorkMock.Object,
            _quotaMock.Object,
            _clinicalContextMock.Object,
            _realtimeNotifierMock.Object);
    }

    // ── builders ─────────────────────────────────────────────────────────────

    private Doctor MakeDoctor(bool isActive = true) =>
        new() { Id = _doctorId, IsActive = isActive, UserId = _doctorUserId };

    private RecoveryPlanRequest MakeRequest(
        RecoveryPlanRequestStatus status = RecoveryPlanRequestStatus.InReview,
        Guid? assignedDoctorId = null) =>
        new()
        {
            Id = _requestId,
            Status = status,
            AssignedDoctorId = assignedDoctorId ?? _doctorId,
            UserSubscriptionId = _subscriptionId,
            UserSubscriptionUsageId = _usageId,
            Version = 1
        };

    private static RecoveryPlanFoodSource MakeFood() =>
        new() { Id = Guid.NewGuid(), FoodName = "Chicken", SortOrder = 0 };

    private static RecoveryPlanNutrientTarget MakeNutrient() =>
        new()
        {
            Id = Guid.NewGuid(),
            NutrientName = "Protein",
            AmountPerDay = 50m,
            Unit = "g",
            SortOrder = 0,
            FoodSources = new List<RecoveryPlanFoodSource> { MakeFood() }
        };

    private static RecoveryPlanPhase MakePhase(int startDay, int endDay) =>
        new()
        {
            Id = Guid.NewGuid(),
            PhaseName = "Phase 1",
            StartDay = startDay,
            EndDay = endDay,
            SleepAndRestHoursPerDay = 10m,
            SortOrder = 0,
            NutrientTargets = new List<RecoveryPlanNutrientTarget> { MakeNutrient() }
        };

    /// <summary>A plan that satisfies ValidateCompletePlan, so publish reaches the quota step.</summary>
    private RecoveryPlan MakeCompletePlan(
        RecoveryPlanStatus status = RecoveryPlanStatus.Draft,
        int durationDays = 7,
        Guid? doctorId = null) =>
        new()
        {
            Id = _planId,
            RecoveryPlanRequestId = _requestId,
            DoctorId = doctorId ?? _doctorId,
            UserId = _userId,
            PlanName = "Recovery Plan",
            Summary = "Get better soon",
            RecheckInstruction = "Check weekly",
            DurationDays = durationDays,
            Status = status,
            Phases = new List<RecoveryPlanPhase> { MakePhase(1, durationDays) }
        };

    private UserSubscriptionUsage MakeUsage() =>
        new() { Id = _usageId, UserSubscriptionId = _subscriptionId, QuotaId = _quotaId };

    /// <summary>Wires the full happy path for PublishAsync; each test overrides the one step it exercises.</summary>
    private void ArrangePublishHappyPath(
        RecoveryPlanRequest? request = null,
        RecoveryPlan? lockedPlan = null,
        RecoveryPlan? trackedPlan = null)
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _planRepoMock.Setup(r => r.GetRequestIdByPlanIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)_requestId);
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request ?? MakeRequest());
        _planRepoMock.Setup(r => r.GetByIdForUpdateAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedPlan ?? MakeCompletePlan());
        _planRepoMock.Setup(r => r.GetTrackedDetailAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackedPlan ?? MakeCompletePlan());
        _clinicalContextMock.Setup(c => c.BuildSnapshotAsync(
                _requestId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryPlanClinicalSnapshot { SchemaVersion = 1, RequestId = _requestId });
        _quotaUsageRepoMock.Setup(r => r.GetByIdForQuotaAsync(
                _usageId, _subscriptionId, RecoveryPlanQuotaService.QuotaCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUsage());
        SetupConsume(QuotaMutationStatus.Applied);
    }

    private void SetupConsume(QuotaMutationStatus status) =>
        _quotaMock.Setup(q => q.ConsumeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

    private void ArrangeStartHappyPath(
        RecoveryPlan? lockedPlan = null,
        RecoveryPlan? trackedPlan = null,
        string? timeZoneId = "UTC")
    {
        _planRepoMock.Setup(r => r.GetByIdForUpdateAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedPlan ?? MakeCompletePlan(RecoveryPlanStatus.ReadyToStart));
        _planRepoMock.Setup(r => r.GetTrackedDetailAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackedPlan ?? MakeCompletePlan(RecoveryPlanStatus.ReadyToStart));
        _planRepoMock.Setup(r => r.GetUserTimeZoneIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(timeZoneId);
    }

    // ── CreateDraftAsync ─────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task CreateDraftAsync_InvalidHeader_ReturnsFail()
    {
        // Arrange
        var req = new CreateRecoveryPlanDraftRequest { PlanName = "", DurationDays = -1 };

        // Act
        var result = await _service.CreateDraftAsync(_doctorId, _requestId, req, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public async Task CreateDraftAsync_DoctorNotFound_ReturnsDoctorProfileRequired()
    {
        // Arrange
        var req = new CreateRecoveryPlanDraftRequest { PlanName = "Plan 1", DurationDays = 30 };
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        // Act
        var result = await _service.CreateDraftAsync(_doctorId, _requestId, req, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("N")]
    public async Task CreateDraftAsync_ValidRequest_Success()
    {
        // Arrange
        var doctor = new Doctor { Id = _doctorId, IsActive = true, UserId = _userId };
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var request = new RecoveryPlanRequest
        {
            Id = _requestId,
            Status = RecoveryPlanRequestStatus.InReview,
            AssignedDoctorId = _doctorId
        };
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _planRepoMock.Setup(r => r.GetByRequestIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlan?)null);

        var req = new CreateRecoveryPlanDraftRequest { PlanName = "Hypertension Care", DurationDays = 30 };

        // Act
        var result = await _service.CreateDraftAsync(_doctorId, _requestId, req, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _planRepoMock.Verify(r => r.Add(It.IsAny<RecoveryPlan>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── PublishAsync: access and lookup guards ───────────────────────────────

    [Test]
    [Category("A")]
    public async Task PublishAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_DoctorInactive_ReturnsDoctorNotActive()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor(isActive: false));

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorNotActive));
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_PlanHasNoRequestId_ReturnsNotFoundWithoutTransaction()
    {
        ArrangePublishHappyPath();
        _planRepoMock.Setup(r => r.GetRequestIdByPlanIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_LockedRequestMissing_RollsBackAndReturnsNotFound()
    {
        ArrangePublishHappyPath();
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlanRequest?)null);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_LockedPlanMissing_RollsBackAndReturnsNotFound()
    {
        ArrangePublishHappyPath();
        _planRepoMock.Setup(r => r.GetByIdForUpdateAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlan?)null);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_PlanBelongsToAnotherDoctor_ReturnsNotFound()
    {
        ArrangePublishHappyPath(lockedPlan: MakeCompletePlan(doctorId: Guid.NewGuid()));

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_RequestAssignedToAnotherDoctor_ReturnsNotFound()
    {
        ArrangePublishHappyPath(request: MakeRequest(assignedDoctorId: Guid.NewGuid()));

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_TrackedPlanMissing_ReturnsNotFound()
    {
        ArrangePublishHappyPath();
        _planRepoMock.Setup(r => r.GetTrackedDetailAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlan?)null);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    // ── PublishAsync: replay and conflict classification ─────────────────────

    [Test]
    [Category("N")]
    public async Task PublishAsync_AlreadyPublished_ReturnsReplayWithoutCommitting()
    {
        ArrangePublishHappyPath(
            request: MakeRequest(RecoveryPlanRequestStatus.Published),
            trackedPlan: MakeCompletePlan(RecoveryPlanStatus.ReadyToStart));

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.IsReplay, Is.True);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_RequestPublishedButPlanStillDraft_ReturnsConflict()
    {
        ArrangePublishHappyPath(
            request: MakeRequest(RecoveryPlanRequestStatus.Published),
            trackedPlan: MakeCompletePlan(RecoveryPlanStatus.Draft));

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.Conflict));
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_PlanPublishedButRequestStillInReview_ReturnsConflict()
    {
        ArrangePublishHappyPath(
            request: MakeRequest(RecoveryPlanRequestStatus.InReview),
            trackedPlan: MakeCompletePlan(RecoveryPlanStatus.Active));

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.Conflict));
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_PlanCancelled_ReturnsRecoveryPlanNotEditable()
    {
        ArrangePublishHappyPath(trackedPlan: MakeCompletePlan(RecoveryPlanStatus.Cancelled));

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanNotEditable));
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_RequestNeedsMoreInformation_ReturnsInvalidRequestState()
    {
        ArrangePublishHappyPath(request: MakeRequest(RecoveryPlanRequestStatus.NeedMoreInformation));

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequestState));
    }

    // ── PublishAsync: structure, snapshot and quota ──────────────────────────

    [Test]
    [Category("A")]
    public async Task PublishAsync_IncompletePlan_ReturnsStructureErrorBeforeConsumingQuota()
    {
        var incomplete = MakeCompletePlan();
        incomplete.RecheckInstruction = null;
        ArrangePublishHappyPath(trackedPlan: incomplete);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
        _quotaMock.Verify(q => q.ConsumeAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_SnapshotUnavailable_ReturnsConflict()
    {
        ArrangePublishHappyPath();
        _clinicalContextMock.Setup(c => c.BuildSnapshotAsync(
                _requestId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlanClinicalSnapshot?)null);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.Conflict));
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_QuotaUsageMissing_ReturnsQuotaMutationFailed()
    {
        ArrangePublishHappyPath();
        _quotaUsageRepoMock.Setup(r => r.GetByIdForQuotaAsync(
                _usageId, _subscriptionId, RecoveryPlanQuotaService.QuotaCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionUsage?)null);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.QuotaMutationFailed));
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_QuotaConsumeRejected_ReturnsQuotaMutationFailed()
    {
        ArrangePublishHappyPath();
        SetupConsume(QuotaMutationStatus.Rejected);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.QuotaMutationFailed));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task PublishAsync_QuotaConsumeDuplicateWithConsistentState_ReturnsReplay()
    {
        ArrangePublishHappyPath();
        SetupConsume(QuotaMutationStatus.Duplicate);

        var published = MakeCompletePlan(RecoveryPlanStatus.ReadyToStart);
        published.RecoveryPlanRequest = MakeRequest(RecoveryPlanRequestStatus.Published);
        _planRepoMock.Setup(r => r.GetDetailByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(published);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.IsReplay, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task PublishAsync_QuotaConsumeDuplicateButStateInconsistent_ReturnsConflict()
    {
        ArrangePublishHappyPath();
        SetupConsume(QuotaMutationStatus.Duplicate);

        // The request never reached Published, so the duplicate cannot be a genuine replay.
        var stalePlan = MakeCompletePlan(RecoveryPlanStatus.ReadyToStart);
        stalePlan.RecoveryPlanRequest = MakeRequest(RecoveryPlanRequestStatus.InReview);
        _planRepoMock.Setup(r => r.GetDetailByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stalePlan);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.Conflict));
    }

    // ── PublishAsync: success ────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task PublishAsync_ValidDraft_MarksPlanReadyToStartAndCommits()
    {
        var plan = MakeCompletePlan();
        ArrangePublishHappyPath(trackedPlan: plan);

        var result = await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.Status, Is.EqualTo(RecoveryPlanStatus.ReadyToStart));
        Assert.That(plan.PublishedAt, Is.Not.Null);
        Assert.That(plan.ClinicalSnapshotJson, Is.EqualTo(SerializedSnapshot));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task PublishAsync_ValidDraft_TransitionsRequestToPublishedAndBumpsVersion()
    {
        var request = MakeRequest();
        ArrangePublishHappyPath(request: request);

        await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(request.Status, Is.EqualTo(RecoveryPlanRequestStatus.Published));
        Assert.That(request.PublishedAt, Is.Not.Null);
        Assert.That(request.AssignmentExpiresAt, Is.Null);
        Assert.That(request.Version, Is.EqualTo(2));
    }

    [Test]
    [Category("N")]
    public async Task PublishAsync_ValidDraft_WritesEventOutboxAndNotifies()
    {
        ArrangePublishHappyPath();

        await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        _requestRepoMock.Verify(r => r.AddEvent(It.IsAny<RecoveryPlanRequestEvent>()), Times.Once);
        _planRepoMock.Verify(r => r.AddOutbox(It.IsAny<OutboxMessage>()), Times.Once);
        _realtimeNotifierMock.Verify(n => n.TryNotifyPlanChangedAsync(
            It.IsAny<RecoveryPlanLifecycleRealtimeNotification>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("B")]
    public async Task PublishAsync_ValidDraft_ClearsSchedulingFieldsSoPlanIsNotYetActive()
    {
        var plan = MakeCompletePlan();
        plan.StartDate = new DateOnly(2026, 1, 1);
        plan.EndDate = new DateOnly(2026, 1, 8);
        plan.ActivatedAt = DateTime.UtcNow;
        plan.IsCurrent = true;
        ArrangePublishHappyPath(trackedPlan: plan);

        await _service.PublishAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(plan.StartDate, Is.Null);
        Assert.That(plan.EndDate, Is.Null);
        Assert.That(plan.ActivatedAt, Is.Null);
        Assert.That(plan.IsCurrent, Is.False);
    }

    // ── StartAsync ───────────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task StartAsync_LockedPlanMissing_ReturnsNotFound()
    {
        _planRepoMock.Setup(r => r.GetByIdForUpdateAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlan?)null);

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task StartAsync_PlanBelongsToAnotherUser_ReturnsNotFound()
    {
        var otherUsersPlan = MakeCompletePlan(RecoveryPlanStatus.ReadyToStart);
        otherUsersPlan.UserId = Guid.NewGuid();
        ArrangeStartHappyPath(lockedPlan: otherUsersPlan);

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("A")]
    public async Task StartAsync_TrackedPlanMissing_ReturnsNotFound()
    {
        ArrangeStartHappyPath();
        _planRepoMock.Setup(r => r.GetTrackedDetailAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlan?)null);

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("B")]
    public async Task StartAsync_PlanHasNoRequestId_ReturnsConflict()
    {
        var orphan = MakeCompletePlan(RecoveryPlanStatus.ReadyToStart);
        orphan.RecoveryPlanRequestId = null;
        ArrangeStartHappyPath(trackedPlan: orphan);

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.Conflict));
    }

    [Test]
    [Category("N")]
    public async Task StartAsync_PlanAlreadyActive_ReturnsReplayWithoutCommitting()
    {
        ArrangeStartHappyPath(trackedPlan: MakeCompletePlan(RecoveryPlanStatus.Active));

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.IsReplay, Is.True);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task StartAsync_PlanStillDraft_ReturnsForbidden()
    {
        ArrangeStartHappyPath(trackedPlan: MakeCompletePlan(RecoveryPlanStatus.Draft));

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.Forbidden));
    }

    [Test]
    [Category("A")]
    public async Task StartAsync_PlanAlreadyCompleted_ReturnsInvalidRequestState()
    {
        ArrangeStartHappyPath(trackedPlan: MakeCompletePlan(RecoveryPlanStatus.Completed));

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequestState));
    }

    [Test]
    [Category("B")]
    public async Task StartAsync_DurationDaysZero_ReturnsInvalidPlanStructure()
    {
        ArrangeStartHappyPath(trackedPlan: MakeCompletePlan(RecoveryPlanStatus.ReadyToStart, durationDays: 0));

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("B")]
    public async Task StartAsync_UserHasNoTimeZone_ReturnsInvalidUserTimeZone()
    {
        ArrangeStartHappyPath(timeZoneId: "   ");

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidUserTimeZone));
    }

    [Test]
    [Category("A")]
    public async Task StartAsync_UnknownTimeZoneId_ReturnsInvalidUserTimeZone()
    {
        ArrangeStartHappyPath(timeZoneId: "Not/A_Real_Zone");

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidUserTimeZone));
    }

    [Test]
    [Category("N")]
    public async Task StartAsync_ReadyToStartPlan_ActivatesAndCommits()
    {
        var plan = MakeCompletePlan(RecoveryPlanStatus.ReadyToStart, durationDays: 7);
        ArrangeStartHappyPath(trackedPlan: plan);

        var result = await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.Status, Is.EqualTo(RecoveryPlanStatus.Active));
        Assert.That(plan.IsCurrent, Is.True);
        Assert.That(plan.ActivatedAt, Is.Not.Null);
        Assert.That(plan.EndDate, Is.EqualTo(plan.StartDate!.Value.AddDays(6)));
        _planRepoMock.Verify(r => r.AddOutbox(It.IsAny<OutboxMessage>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("B")]
    public async Task StartAsync_SingleDayPlan_EndDateEqualsStartDate()
    {
        var plan = MakeCompletePlan(RecoveryPlanStatus.ReadyToStart, durationDays: 1);
        ArrangeStartHappyPath(trackedPlan: plan);

        await _service.StartAsync(_userId, _planId, CancellationToken.None);

        Assert.That(plan.EndDate, Is.EqualTo(plan.StartDate));
    }

    // ── draft-write helpers (Update/DeleteDraft, Phase/Food CRUD share ExecuteDraftWriteAsync) ──

    private void ArrangeDraftWriteHappyPath(
        RecoveryPlanRequest? request = null,
        RecoveryPlan? lockedPlan = null,
        RecoveryPlan? trackedPlan = null)
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _planRepoMock.Setup(r => r.GetRequestIdByPlanIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)_requestId);
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request ?? MakeRequest());
        _planRepoMock.Setup(r => r.GetByIdForUpdateAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedPlan ?? MakeCompletePlan());
        _planRepoMock.Setup(r => r.GetTrackedDetailAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackedPlan ?? MakeCompletePlan());
    }

    // ── UpdateDraftAsync ─────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task UpdateDraftAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);
        var request = new UpdateRecoveryPlanDraftRequest { PlanName = "Plan", DurationDays = 7 };

        var result = await _service.UpdateDraftAsync(_doctorUserId, _planId, request, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task UpdateDraftAsync_ValidRequest_UpdatesHeaderAndCommits()
    {
        var plan = MakeCompletePlan();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);
        var request = new UpdateRecoveryPlanDraftRequest
        {
            PlanName = "Updated Plan",
            Summary = "Updated summary",
            DurationDays = 7,
            RecheckInstruction = "Check monthly"
        };

        var result = await _service.UpdateDraftAsync(_doctorUserId, _planId, request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.PlanName, Is.EqualTo("Updated Plan"));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeleteDraftAsync ─────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task DeleteDraftAsync_RequestNotEditable_ReturnsInvalidRequestState()
    {
        ArrangeDraftWriteHappyPath(request: MakeRequest(RecoveryPlanRequestStatus.Assigned));

        var result = await _service.DeleteDraftAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequestState));
    }

    [Test]
    [Category("N")]
    public async Task DeleteDraftAsync_ValidRequest_SoftDeletesPlanAndCommits()
    {
        var plan = MakeCompletePlan();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);

        var result = await _service.DeleteDraftAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.IsDeleted, Is.True);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CreatePhaseAsync ─────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task CreatePhaseAsync_PlanNotDraft_ReturnsRecoveryPlanNotEditable()
    {
        ArrangeDraftWriteHappyPath(lockedPlan: MakeCompletePlan(RecoveryPlanStatus.ReadyToStart));
        var request = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Phase 2",
            StartDay = 8,
            EndDay = 14,
            SortOrder = 1
        };

        var result = await _service.CreatePhaseAsync(_doctorUserId, _planId, request, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanNotEditable));
    }

    [Test]
    [Category("N")]
    public async Task CreatePhaseAsync_ValidRequest_AddsPhaseAndCommits()
    {
        var plan = MakeCompletePlan();
        plan.Phases = new List<RecoveryPlanPhase>();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);
        var request = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Phase 1",
            StartDay = 1,
            EndDay = 7,
            SortOrder = 0
        };

        var result = await _service.CreatePhaseAsync(_doctorUserId, _planId, request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.Phases, Has.Count.EqualTo(1));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdatePhaseAsync ─────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task UpdatePhaseAsync_PhaseNotFound_ReturnsNotFound()
    {
        ArrangeDraftWriteHappyPath();
        var request = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Phase 1",
            StartDay = 1,
            EndDay = 7,
            SortOrder = 0
        };

        var result = await _service.UpdatePhaseAsync(
            _doctorUserId, _planId, Guid.NewGuid(), request, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task UpdatePhaseAsync_ValidRequest_UpdatesPhaseAndCommits()
    {
        var plan = MakeCompletePlan();
        var phase = plan.Phases.First();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);
        var request = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Renamed Phase",
            StartDay = 1,
            EndDay = 7,
            SortOrder = 0
        };

        var result = await _service.UpdatePhaseAsync(
            _doctorUserId, _planId, phase.Id, request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(phase.PhaseName, Is.EqualTo("Renamed Phase"));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeletePhaseAsync ─────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task DeletePhaseAsync_DoctorOwnershipMismatch_ReturnsNotFound()
    {
        ArrangeDraftWriteHappyPath(lockedPlan: MakeCompletePlan(doctorId: Guid.NewGuid()));

        var result = await _service.DeletePhaseAsync(
            _doctorUserId, _planId, Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task DeletePhaseAsync_ValidRequest_SoftDeletesPhaseAndCommits()
    {
        var plan = MakeCompletePlan();
        var phase = plan.Phases.First();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);

        var result = await _service.DeletePhaseAsync(_doctorUserId, _planId, phase.Id, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(phase.IsDeleted, Is.True);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CreateFoodAsync ──────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task CreateFoodAsync_NutrientNotFound_ReturnsNotFound()
    {
        var plan = MakeCompletePlan();
        var phase = plan.Phases.First();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);
        var request = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Rice", SortOrder = 1 };

        var result = await _service.CreateFoodAsync(
            _doctorUserId, _planId, phase.Id, Guid.NewGuid(), request, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task CreateFoodAsync_ValidRequest_AddsFoodAndCommits()
    {
        var plan = MakeCompletePlan();
        var phase = plan.Phases.First();
        var nutrient = phase.NutrientTargets.First();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);
        var request = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Rice", SortOrder = 1 };

        var result = await _service.CreateFoodAsync(
            _doctorUserId, _planId, phase.Id, nutrient.Id, request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(nutrient.FoodSources, Has.Count.EqualTo(2));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateFoodAsync ──────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task UpdateFoodAsync_FoodNotFound_ReturnsNotFound()
    {
        var plan = MakeCompletePlan();
        var phase = plan.Phases.First();
        var nutrient = phase.NutrientTargets.First();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);
        var request = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Rice", SortOrder = 0 };

        var result = await _service.UpdateFoodAsync(
            _doctorUserId, _planId, phase.Id, nutrient.Id, Guid.NewGuid(), request, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task UpdateFoodAsync_ValidRequest_UpdatesFoodAndCommits()
    {
        var plan = MakeCompletePlan();
        var phase = plan.Phases.First();
        var nutrient = phase.NutrientTargets.First();
        var food = nutrient.FoodSources.First();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);
        var request = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Grilled Chicken", SortOrder = 0 };

        var result = await _service.UpdateFoodAsync(
            _doctorUserId, _planId, phase.Id, nutrient.Id, food.Id, request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(food.FoodName, Is.EqualTo("Grilled Chicken"));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeleteFoodAsync ──────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task DeleteFoodAsync_PlanHasNoRequestId_ReturnsNotFoundWithoutTransaction()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _planRepoMock.Setup(r => r.GetRequestIdByPlanIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var result = await _service.DeleteFoodAsync(
            _doctorUserId, _planId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task DeleteFoodAsync_ValidRequest_SoftDeletesFoodAndCommits()
    {
        var plan = MakeCompletePlan();
        var phase = plan.Phases.First();
        var nutrient = phase.NutrientTargets.First();
        var food = nutrient.FoodSources.First();
        ArrangeDraftWriteHappyPath(trackedPlan: plan);

        var result = await _service.DeleteFoodAsync(
            _doctorUserId, _planId, phase.Id, nutrient.Id, food.Id, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(food.IsDeleted, Is.True);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetMineAsync ─────────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetMineAsync_InvalidStatusEnum_ReturnsInvalidRequest()
    {
        var result = await _service.GetMineAsync(
            _userId, new PaginationQuery(), (RecoveryPlanStatus)999, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("B")]
    public async Task GetMineAsync_StatusDraft_ReturnsForbidden()
    {
        var result = await _service.GetMineAsync(
            _userId, new PaginationQuery(), RecoveryPlanStatus.Draft, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.Forbidden));
    }

    [Test]
    [Category("N")]
    public async Task GetMineAsync_ValidRequest_ReturnsPagedSummaries()
    {
        var page = new PaginationQuery { PageNumber = 1, PageSize = 10 };
        var plan = MakeCompletePlan(RecoveryPlanStatus.Active);
        _planRepoMock.Setup(r => r.GetUserPlansPagedAsync(
                _userId, 1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RecoveryPlan>
            {
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 1,
                TotalPages = 1,
                Items = new List<RecoveryPlan> { plan }
            });

        var result = await _service.GetMineAsync(_userId, page, null, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Items, Has.Count.EqualTo(1));
        Assert.That(result.Data.Items[0].Id, Is.EqualTo(plan.Id));
    }

    // ── GetUserDetailAsync ───────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetUserDetailAsync_PlanNotFound_ReturnsNotFound()
    {
        _planRepoMock.Setup(r => r.GetDetailByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlan?)null);

        var result = await _service.GetUserDetailAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("A")]
    public async Task GetUserDetailAsync_PlanBelongsToAnotherUser_ReturnsNotFound()
    {
        var plan = MakeCompletePlan(RecoveryPlanStatus.Active);
        plan.UserId = Guid.NewGuid();
        _planRepoMock.Setup(r => r.GetDetailByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetUserDetailAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("B")]
    public async Task GetUserDetailAsync_PlanStillDraft_ReturnsNotFound()
    {
        var plan = MakeCompletePlan(RecoveryPlanStatus.Draft);
        _planRepoMock.Setup(r => r.GetDetailByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetUserDetailAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task GetUserDetailAsync_ValidRequest_ReturnsDetail()
    {
        var plan = MakeCompletePlan(RecoveryPlanStatus.Active);
        _planRepoMock.Setup(r => r.GetDetailByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetUserDetailAsync(_userId, _planId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Id, Is.EqualTo(plan.Id));
    }

    // ── GetDoctorDetailAsync ─────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetDoctorDetailAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.GetDoctorDetailAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("A")]
    public async Task GetDoctorDetailAsync_PlanNotOwnedByDoctor_ReturnsNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        var plan = MakeCompletePlan(RecoveryPlanStatus.Active, doctorId: Guid.NewGuid());
        plan.RecoveryPlanRequest = MakeRequest(assignedDoctorId: _doctorId);
        _planRepoMock.Setup(r => r.GetDetailByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _service.GetDoctorDetailAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task GetDoctorDetailAsync_ValidRequest_ReturnsDoctorDetailWithSnapshot()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        var plan = MakeCompletePlan(RecoveryPlanStatus.Active);
        plan.ClinicalSnapshotJson = SerializedSnapshot;
        plan.RecoveryPlanRequest = MakeRequest(assignedDoctorId: _doctorId);
        _planRepoMock.Setup(r => r.GetDetailByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _clinicalContextMock.Setup(c => c.DeserializeSnapshot(SerializedSnapshot))
            .Returns(new RecoveryPlanClinicalSnapshot { SchemaVersion = 1, RequestId = _requestId });

        var result = await _service.GetDoctorDetailAsync(_doctorUserId, _planId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.RequestId, Is.EqualTo(_requestId));
        Assert.That(result.Data.ClinicalSnapshot, Is.Not.Null);
    }
}

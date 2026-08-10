using MedMateAI.Application.Common;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlanRequests;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Application.Options;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanRequestServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IRecoveryPlanRequestRepository> _requestRepoMock = null!;
    private Mock<IRecoveryPlanRepository> _planRepoMock = null!;
    private Mock<IQuotaUsageRepository> _quotaUsageRepoMock = null!;
    private Mock<IRecoveryPlanQuotaService> _quotaMock = null!;
    private Mock<IRecoveryPlanRealtimeNotifier> _realtimeNotifierMock = null!;
    private Mock<IOptions<RecoveryPlanOptions>> _optionsMock = null!;
    private RecoveryPlanRequestService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _doctorUserId = Guid.NewGuid();
    private readonly Guid _requestId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _requestRepoMock = new Mock<IRecoveryPlanRequestRepository>();
        _planRepoMock = new Mock<IRecoveryPlanRepository>();
        _quotaUsageRepoMock = new Mock<IQuotaUsageRepository>();
        _quotaMock = new Mock<IRecoveryPlanQuotaService>();
        _realtimeNotifierMock = new Mock<IRecoveryPlanRealtimeNotifier>();
        _optionsMock = new Mock<IOptions<RecoveryPlanOptions>>();

        _unitOfWorkMock.Setup(u => u.RecoveryPlanRequests).Returns(_requestRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.RecoveryPlans).Returns(_planRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.QuotaUsages).Returns(_quotaUsageRepoMock.Object);

        _realtimeNotifierMock.Setup(n => n.TryNotifyRequestChangedAsync(
                It.IsAny<RecoveryPlanRequestRealtimeNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Transaction setups
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _optionsMock.Setup(o => o.Value).Returns(new RecoveryPlanOptions { AssignmentTimeoutMinutes = 15 });

        _service = new RecoveryPlanRequestService(
            _unitOfWorkMock.Object,
            _quotaMock.Object,
            _realtimeNotifierMock.Object,
            _optionsMock.Object);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task CreateAsync_InvalidIdempotencyKey_ReturnsFail()
    {
        // Act
        var result = await _service.CreateAsync(_userId, "", new CreateRecoveryPlanRequest(), CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.IdempotencyKeyInvalid));
    }

    [Test]
    [Category("A")]
    public async Task CreateAsync_InvalidDiseaseGroup_ReturnsFail()
    {
        // Arrange
        var req = new CreateRecoveryPlanRequest { DiseaseGroup = (RecoveryPlanDiseaseGroup)99 };

        // Act
        var result = await _service.CreateAsync(_userId, "valid-key", req, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("N")]
    public async Task CreateAsync_ValidRequest_Success()
    {
        // Arrange
        var req = new CreateRecoveryPlanRequest
        {
            DiseaseGroup = RecoveryPlanDiseaseGroup.Respiratory,
            RequestNote = "Notes here"
        };

        // LoadIdempotentReplayAsync needs QuotaUsages.GetLogByIdempotencyKeyAsync to return null (no replay)
        _quotaUsageRepoMock.Setup(q => q.GetLogByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscriptionLog?)null);

        // Quota is resolved successfully
        var usageResult = RecoveryPlanOperationResult<UserSubscriptionUsage>.Ok(new UserSubscriptionUsage
        {
            Id = Guid.NewGuid(),
            UserSubscriptionId = Guid.NewGuid()
        });
        _quotaMock.Setup(q => q.ResolveUsageAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageResult);

        // Act
        var result = await _service.CreateAsync(_userId, "valid-key", req, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _requestRepoMock.Verify(r => r.Add(It.IsAny<RecoveryPlanRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── StartReviewAsync & Transition ────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task StartReviewAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        // Arrange — DoctorTransitionAsync looks up doctor first, returns DoctorProfileNotFound if null
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        // Act
        var result = await _service.StartReviewAsync(_doctorId, _requestId, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("A")]
    public async Task StartReviewAsync_RequestNotFound_ReturnsNotFound()
    {
        // Arrange — doctor found, but request not found
        var doctor = new Doctor { Id = _doctorId, UserId = _userId, IsActive = true };
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlanRequest?)null);

        // Act
        var result = await _service.StartReviewAsync(_userId, _requestId, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task StartReviewAsync_ValidAssignedRequest_TransitionsToInReview()
    {
        // Arrange
        var doctor = new Doctor { Id = _doctorId, UserId = _userId, IsActive = true };
        var request = new RecoveryPlanRequest
        {
            Id = _requestId,
            Status = RecoveryPlanRequestStatus.Assigned,
            AssignedDoctorId = _doctorId,
            AssignmentExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        // Act
        var result = await _service.StartReviewAsync(_userId, _requestId, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(request.Status, Is.EqualTo(RecoveryPlanRequestStatus.InReview));

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── builders ─────────────────────────────────────────────────────────────

    private Doctor MakeDoctor(
        bool isActive = true,
        bool isAccepting = true,
        int? maxConcurrent = null) =>
        new()
        {
            Id = _doctorId,
            UserId = _doctorUserId,
            IsActive = isActive,
            IsAcceptingRecoveryPlanRequests = isAccepting,
            MaxConcurrentRecoveryPlanRequests = maxConcurrent
        };

    private RecoveryPlanRequest MakeRequest(
        RecoveryPlanRequestStatus status = RecoveryPlanRequestStatus.WaitingForDoctor,
        Guid? assignedDoctorId = null,
        Guid? userId = null) =>
        new()
        {
            Id = _requestId,
            UserId = userId ?? _userId,
            Status = status,
            AssignedDoctorId = assignedDoctorId,
            UserSubscriptionId = Guid.NewGuid(),
            UserSubscriptionUsageId = Guid.NewGuid(),
            Version = 1
        };

    // ── GetMineAsync ─────────────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task GetMineAsync_ValidRequest_ReturnsPagedRequests()
    {
        var page = new PaginationQuery { PageNumber = 1, PageSize = 10 };
        _requestRepoMock.Setup(r => r.GetByUserPagedAsync(_userId, 1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RecoveryPlanRequest>
            {
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 1,
                TotalPages = 1,
                Items = new List<RecoveryPlanRequest> { MakeRequest() }
            });

        var result = await _service.GetMineAsync(_userId, page, null, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Items, Has.Count.EqualTo(1));
        Assert.That(result.Data.Items[0].Id, Is.EqualTo(_requestId));
    }

    // ── GetDetailAsync ───────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetDetailAsync_RequestNotFound_ReturnsNotFound()
    {
        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlanRequest?)null);

        var result = await _service.GetDetailAsync(_userId, false, false, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task GetDetailAsync_OwnerUser_ReturnsDetail()
    {
        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest());

        var result = await _service.GetDetailAsync(_userId, false, false, _requestId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Id, Is.EqualTo(_requestId));
    }

    [Test]
    [Category("A")]
    public async Task GetDetailAsync_NotOwnerNotDoctorNotAdmin_ReturnsNotFound()
    {
        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest());

        var result = await _service.GetDetailAsync(Guid.NewGuid(), false, false, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task GetDetailAsync_AssignedDoctorAccess_ReturnsDetail()
    {
        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.InReview, assignedDoctorId: _doctorId));
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());

        var result = await _service.GetDetailAsync(_doctorUserId, true, false, _requestId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
    }

    // ── CancelAsync ──────────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task CancelAsync_RequestNotFound_ReturnsNotFound()
    {
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlanRequest?)null);

        var result = await _service.CancelAsync(_userId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("A")]
    public async Task CancelAsync_StatusNotCancellable_ReturnsInvalidRequestState()
    {
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.Published));

        var result = await _service.CancelAsync(_userId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequestState));
    }

    [Test]
    [Category("N")]
    public async Task CancelAsync_ValidRequest_CancelsAndReleasesQuota()
    {
        var request = MakeRequest(RecoveryPlanRequestStatus.WaitingForDoctor);
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _quotaUsageRepoMock.Setup(q => q.GetByIdAsync(request.UserSubscriptionUsageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscriptionUsage { Id = request.UserSubscriptionUsageId, QuotaId = Guid.NewGuid() });
        _quotaMock.Setup(q => q.ReleaseAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Applied);

        var result = await _service.CancelAsync(_userId, _requestId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(request.Status, Is.EqualTo(RecoveryPlanRequestStatus.Cancelled));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ProvideInformationAsync ──────────────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task ProvideInformationAsync_InformationEmpty_ReturnsInvalidRequest()
    {
        var result = await _service.ProvideInformationAsync(_userId, _requestId, "  ", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public async Task ProvideInformationAsync_StatusNotNeedMoreInformation_ReturnsInvalidRequestState()
    {
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.InReview));

        var result = await _service.ProvideInformationAsync(_userId, _requestId, "More info", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequestState));
    }

    [Test]
    [Category("N")]
    public async Task ProvideInformationAsync_ValidRequest_TransitionsToInReview()
    {
        var request = MakeRequest(RecoveryPlanRequestStatus.NeedMoreInformation);
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _service.ProvideInformationAsync(_userId, _requestId, "More info", CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(request.Status, Is.EqualTo(RecoveryPlanRequestStatus.InReview));
        Assert.That(request.RequestNote, Is.EqualTo("More info"));
    }

    // ── GetOpenAsync ─────────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetOpenAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.GetOpenAsync(
            _doctorUserId, new PaginationQuery(), null, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("N")]
    public async Task GetOpenAsync_ValidRequest_ReturnsOpenRequests()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        var page = new PaginationQuery { PageNumber = 1, PageSize = 10 };
        _requestRepoMock.Setup(r => r.GetOpenPagedAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RecoveryPlanRequest>
            {
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 1,
                TotalPages = 1,
                Items = new List<RecoveryPlanRequest> { MakeRequest() }
            });

        var result = await _service.GetOpenAsync(_doctorUserId, page, null, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Items, Has.Count.EqualTo(1));
    }

    // ── GetDoctorDetailAsync ─────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetDoctorDetailAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.GetDoctorDetailAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("A")]
    public async Task GetDoctorDetailAsync_RequestNotAssignedToDoctor_ReturnsNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetAssignedToDoctorByIdAsync(_doctorId, _requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DoctorRecoveryPlanRequestData?)null);

        var result = await _service.GetDoctorDetailAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task GetDoctorDetailAsync_ValidRequest_ReturnsDetail()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        var data = new DoctorRecoveryPlanRequestData(
            _requestId, _userId, _doctorId, RecoveryPlanDiseaseGroup.Respiratory, null, null,
            RecoveryPlanRequestStatus.InReview, null, DateTime.UtcNow, null, null, null, null, null,
            null, null, 1, null, null);
        _requestRepoMock.Setup(r => r.GetAssignedToDoctorByIdAsync(_doctorId, _requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var result = await _service.GetDoctorDetailAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Id, Is.EqualTo(_requestId));
    }

    // ── GetDoctorMineAsync ───────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetDoctorMineAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.GetDoctorMineAsync(
            _doctorUserId, new PaginationQuery(), null, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("N")]
    public async Task GetDoctorMineAsync_ValidRequest_ReturnsPagedRequests()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        var page = new PaginationQuery { PageNumber = 1, PageSize = 10 };
        var data = new DoctorRecoveryPlanRequestData(
            _requestId, _userId, _doctorId, RecoveryPlanDiseaseGroup.Respiratory, null, null,
            RecoveryPlanRequestStatus.InReview, null, DateTime.UtcNow, null, null, null, null, null,
            null, null, 1, null, null);
        _requestRepoMock.Setup(r => r.GetAssignedToDoctorPagedAsync(_doctorId, 1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<DoctorRecoveryPlanRequestData>
            {
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 1,
                TotalPages = 1,
                Items = new List<DoctorRecoveryPlanRequestData> { data }
            });

        var result = await _service.GetDoctorMineAsync(_doctorUserId, page, null, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Items, Has.Count.EqualTo(1));
    }

    // ── AcceptAsync ──────────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task AcceptAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdForUpdateAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.AcceptAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("A")]
    public async Task AcceptAsync_RequestAlreadyClaimedByAnotherDoctor_ReturnsAlreadyClaimed()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdForUpdateAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.Assigned, assignedDoctorId: Guid.NewGuid()));

        var result = await _service.AcceptAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanRequestAlreadyClaimed));
    }

    [Test]
    [Category("B")]
    public async Task AcceptAsync_DoctorCapacityReached_ReturnsDoctorCapacityReached()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdForUpdateAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor(maxConcurrent: 1));
        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.WaitingForDoctor));
        _requestRepoMock.Setup(r => r.CountActiveAssignmentsAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.AcceptAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorCapacityReached));
    }

    [Test]
    [Category("N")]
    public async Task AcceptAsync_ValidRequest_AssignsDoctorAndCommits()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdForUpdateAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.WaitingForDoctor));
        _requestRepoMock.Setup(r => r.CountActiveAssignmentsAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var accepted = MakeRequest(RecoveryPlanRequestStatus.Assigned, assignedDoctorId: _doctorId);
        _requestRepoMock.Setup(r => r.TryAcceptAsync(
                _requestId, _doctorId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(accepted);

        var result = await _service.AcceptAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        _requestRepoMock.Verify(r => r.AddEvent(It.IsAny<RecoveryPlanRequestEvent>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ReleaseAsync ─────────────────────────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task ReleaseAsync_ReasonTooLong_ReturnsInvalidRequest()
    {
        var result = await _service.ReleaseAsync(
            _doctorUserId, _requestId, new string('a', 2001), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public async Task ReleaseAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.ReleaseAsync(_doctorUserId, _requestId, "reason", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("A")]
    public async Task ReleaseAsync_StatusNotReleasable_ReturnsInvalidRequestState()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.WaitingForDoctor, assignedDoctorId: _doctorId));

        var result = await _service.ReleaseAsync(_doctorUserId, _requestId, "reason", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequestState));
    }

    [Test]
    [Category("N")]
    public async Task ReleaseAsync_ValidRequest_ReleasesAndReturnsToWaiting()
    {
        var request = MakeRequest(RecoveryPlanRequestStatus.Assigned, assignedDoctorId: _doctorId);
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _planRepoMock.Setup(r => r.GetActivePlanIdByRequestIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var result = await _service.ReleaseAsync(_doctorUserId, _requestId, "Too busy", CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(request.Status, Is.EqualTo(RecoveryPlanRequestStatus.WaitingForDoctor));
        Assert.That(request.AssignedDoctorId, Is.Null);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── RequestInformationAsync ──────────────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task RequestInformationAsync_ReasonEmpty_ReturnsInvalidRequest()
    {
        var result = await _service.RequestInformationAsync(_doctorUserId, _requestId, "  ", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public async Task RequestInformationAsync_StatusNotInReview_ReturnsInvalidRequestState()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.Assigned, assignedDoctorId: _doctorId));

        var result = await _service.RequestInformationAsync(_doctorUserId, _requestId, "Need more", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequestState));
    }

    [Test]
    [Category("N")]
    public async Task RequestInformationAsync_ValidRequest_TransitionsToNeedMoreInformation()
    {
        var request = MakeRequest(RecoveryPlanRequestStatus.InReview, assignedDoctorId: _doctorId);
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _service.RequestInformationAsync(_doctorUserId, _requestId, "Need labs", CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(request.Status, Is.EqualTo(RecoveryPlanRequestStatus.NeedMoreInformation));
    }

    // ── RejectAsync ──────────────────────────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task RejectAsync_CodeOrReasonEmpty_ReturnsInvalidRequest()
    {
        var result = await _service.RejectAsync(_doctorUserId, _requestId, "", "reason", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public async Task RejectAsync_StatusNotRejectable_ReturnsInvalidRequestState()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(RecoveryPlanRequestStatus.WaitingForDoctor, assignedDoctorId: _doctorId));

        var result = await _service.RejectAsync(
            _doctorUserId, _requestId, "CODE1", "Not eligible", CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequestState));
    }

    [Test]
    [Category("N")]
    public async Task RejectAsync_ValidRequest_RejectsAndReleasesQuota()
    {
        var request = MakeRequest(RecoveryPlanRequestStatus.InReview, assignedDoctorId: _doctorId);
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _quotaUsageRepoMock.Setup(q => q.GetByIdAsync(request.UserSubscriptionUsageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscriptionUsage { Id = request.UserSubscriptionUsageId, QuotaId = Guid.NewGuid() });
        _quotaMock.Setup(q => q.ReleaseAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Applied);

        var result = await _service.RejectAsync(
            _doctorUserId, _requestId, "CODE1", "Not eligible", CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(request.Status, Is.EqualTo(RecoveryPlanRequestStatus.Rejected));
        Assert.That(request.RejectionReasonCode, Is.EqualTo("CODE1"));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

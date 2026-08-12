using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.ServiceCredits;
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
public class ServiceCreditServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IQuotaUsageRepository> _quotaUsageRepositoryMock = null!;
    private Mock<ISubscriptionPlanQuotaRepository> _subscriptionPlanQuotaRepositoryMock = null!;
    private ServiceCreditService _service = null!;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _referenceId = Guid.NewGuid();
    private readonly Guid _actorUserId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _quotaUsageRepositoryMock = new Mock<IQuotaUsageRepository>();
        _subscriptionPlanQuotaRepositoryMock = new Mock<ISubscriptionPlanQuotaRepository>();

        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.QuotaUsages)
            .Returns(_quotaUsageRepositoryMock.Object);

        _service = new ServiceCreditService(
            _unitOfWorkMock.Object,
            _subscriptionPlanQuotaRepositoryMock.Object);
    }

    [Test]
    public async Task GetBalanceAsync_MultiplePackages_AggregatesServiceCredits()
    {
        var usages = new List<UserSubscriptionUsage>
        {
            MakeUsage(limitValue: 5, usedCount: 2, reservedCount: 1),
            MakeUsage(limitValue: 10, usedCount: 3, reservedCount: 2)
        };
        _quotaUsageRepositoryMock.Setup(repository => repository.GetEligibleByUserAsync(
                _userId,
                IServiceCreditService.QuotaCode,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(usages);

        var result = await _service.GetBalanceAsync(_userId, _utcNow, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.QuotaCode, Is.EqualTo(IServiceCreditService.QuotaCode));
            Assert.That(result.Data.GrantedCount, Is.EqualTo(15));
            Assert.That(result.Data.UsedCount, Is.EqualTo(5));
            Assert.That(result.Data.ReservedCount, Is.EqualTo(3));
            Assert.That(result.Data.RemainingCount, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task ReserveAsync_ExistingLog_AcquiresLockBeforeLookupAndReturnsReplay()
    {
        const string idempotencyKey = "reserve-existing";
        var usage = MakeUsage();
        var existingLog = new UserSubscriptionLog { UserSubscriptionUsageId = usage.Id };
        var calls = new List<string>();

        _quotaUsageRepositoryMock.Setup(repository => repository.AcquireIdempotencyLockAsync(
                idempotencyKey,
                CancellationToken.None))
            .Callback(() => calls.Add("lock"))
            .Returns(Task.CompletedTask);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetLogByIdempotencyKeyAsync(
                idempotencyKey,
                CancellationToken.None))
            .Callback(() => calls.Add("log"))
            .ReturnsAsync(existingLog);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetByIdAsync(
                usage.Id,
                CancellationToken.None))
            .ReturnsAsync(usage);

        var result = await ReserveAsync(idempotencyKey);

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(new[] { "lock", "log" }));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.SameAs(usage));
            Assert.That(result.IsReplay, Is.True);
        });
        _quotaUsageRepositoryMock.Verify(repository => repository.ReserveAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReserveAsync_ExistingLogWithoutUsage_ReturnsQuotaMutationFailed()
    {
        const string idempotencyKey = "reserve-missing-usage";
        var usageId = Guid.NewGuid();
        SetupIdempotency(idempotencyKey, new UserSubscriptionLog
        {
            UserSubscriptionUsageId = usageId
        });
        _quotaUsageRepositoryMock.Setup(repository => repository.GetByIdAsync(
                usageId,
                CancellationToken.None))
            .ReturnsAsync((UserSubscriptionUsage?)null);

        var result = await ReserveAsync(idempotencyKey);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(ServiceCreditErrorCode.QuotaMutationFailed));
        });
        _quotaUsageRepositoryMock.Verify(repository => repository.ReserveAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReserveAsync_MissingQuotaDefinition_ReturnsServiceCreditNotConfigured()
    {
        const string idempotencyKey = "reserve-no-definition";
        SetupIdempotency(idempotencyKey);
        _subscriptionPlanQuotaRepositoryMock.Setup(repository => repository.GetQuotaDefinitionByCodeAsync(
                IServiceCreditService.QuotaCode,
                CancellationToken.None))
            .ReturnsAsync((Quota?)null);

        var result = await ReserveAsync(idempotencyKey);

        Assert.That(result.Error, Is.EqualTo(ServiceCreditErrorCode.ServiceCreditNotConfigured));
        _quotaUsageRepositoryMock.Verify(repository => repository.GetEligibleByUserAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReserveAsync_InactiveQuotaDefinition_ReturnsServiceCreditNotConfigured()
    {
        const string idempotencyKey = "reserve-inactive-definition";
        SetupIdempotency(idempotencyKey);
        SetupQuotaDefinition(isActive: false);

        var result = await ReserveAsync(idempotencyKey);

        Assert.That(result.Error, Is.EqualTo(ServiceCreditErrorCode.ServiceCreditNotConfigured));
    }

    [Test]
    public async Task ReserveAsync_NoEligiblePackage_ReturnsNoCreditPackage()
    {
        const string idempotencyKey = "reserve-no-package";
        SetupIdempotency(idempotencyKey);
        SetupQuotaDefinition();
        _quotaUsageRepositoryMock.Setup(repository => repository.GetEligibleByUserAsync(
                _userId,
                IServiceCreditService.QuotaCode,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(Array.Empty<UserSubscriptionUsage>());

        var result = await ReserveAsync(idempotencyKey);

        Assert.That(result.Error, Is.EqualTo(ServiceCreditErrorCode.NoCreditPackage));
    }

    [Test]
    public async Task ReserveAsync_AllPackagesExhausted_ReturnsServiceCreditExhausted()
    {
        const string idempotencyKey = "reserve-exhausted";
        SetupIdempotency(idempotencyKey);
        SetupQuotaDefinition();
        _quotaUsageRepositoryMock.Setup(repository => repository.GetEligibleByUserAsync(
                _userId,
                IServiceCreditService.QuotaCode,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(new[]
            {
                MakeUsage(limitValue: 5, usedCount: 4, reservedCount: 1),
                MakeUsage(limitValue: 10, usedCount: 8, reservedCount: 2)
            });

        var result = await ReserveAsync(idempotencyKey);

        Assert.That(result.Error, Is.EqualTo(ServiceCreditErrorCode.ServiceCreditExhausted));
        _quotaUsageRepositoryMock.Verify(repository => repository.ReserveAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReserveAsync_CandidateSucceeds_InsertsReserveLogAndReturnsUsage()
    {
        const string idempotencyKey = "reserve-success";
        const string referenceType = "RecoveryPlanRequest";
        const string reason = "test reserve";
        var candidate = MakeUsage(limitValue: 5, usedCount: 1, reservedCount: 1);
        var mutation = MakeMutation(candidate, usedAfter: 1, reservedAfter: 2);
        UserSubscriptionLog? insertedLog = null;

        SetupReserveCandidates(idempotencyKey, new[] { candidate });
        _quotaUsageRepositoryMock.Setup(repository => repository.ReserveAsync(
                candidate.Id,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(mutation);
        _quotaUsageRepositoryMock.Setup(repository => repository.TryInsertLogAsync(
                It.IsAny<UserSubscriptionLog>(),
                CancellationToken.None))
            .Callback<UserSubscriptionLog, CancellationToken>((log, _) => insertedLog = log)
            .ReturnsAsync(true);

        var result = await _service.ReserveAsync(
            _userId,
            referenceType,
            _referenceId,
            _actorUserId,
            idempotencyKey,
            reason,
            _utcNow,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.SameAs(candidate));
            Assert.That(result.IsReplay, Is.False);
            Assert.That(insertedLog, Is.Not.Null);
            Assert.That(insertedLog!.ActionType, Is.EqualTo(SubscriptionQuotaActionType.Reserve));
            Assert.That(insertedLog.Quantity, Is.EqualTo(1));
            Assert.That(insertedLog.ReferenceType, Is.EqualTo(referenceType));
            Assert.That(insertedLog.ReferenceId, Is.EqualTo(_referenceId));
            Assert.That(insertedLog.IdempotencyKey, Is.EqualTo(idempotencyKey));
            Assert.That(insertedLog.PerformedByUserId, Is.EqualTo(_actorUserId));
            Assert.That(insertedLog.UserSubscriptionUsageId, Is.EqualTo(candidate.Id));
            Assert.That(insertedLog.UserSubscriptionId, Is.EqualTo(candidate.UserSubscriptionId));
            Assert.That(insertedLog.QuotaId, Is.EqualTo(candidate.QuotaId));
            Assert.That(insertedLog.CreatedAt, Is.EqualTo(_utcNow));
        });
    }

    [Test]
    public async Task ReserveAsync_DuplicateLogInsert_ReturnsSuccessAsReplay()
    {
        const string idempotencyKey = "reserve-concurrent-duplicate";
        var candidate = MakeUsage();
        SetupReserveCandidates(idempotencyKey, new[] { candidate });
        _quotaUsageRepositoryMock.Setup(repository => repository.ReserveAsync(
                candidate.Id,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(MakeMutation(candidate));
        _quotaUsageRepositoryMock.Setup(repository => repository.TryInsertLogAsync(
                It.IsAny<UserSubscriptionLog>(),
                CancellationToken.None))
            .ReturnsAsync(false);

        var result = await ReserveAsync(idempotencyKey);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.SameAs(candidate));
            Assert.That(result.IsReplay, Is.True);
        });
    }

    [Test]
    public async Task ReserveAsync_FirstCandidateFails_TriesNextCandidate()
    {
        const string idempotencyKey = "reserve-next-candidate";
        var first = MakeUsage();
        var second = MakeUsage();
        SetupReserveCandidates(idempotencyKey, new[] { first, second });
        _quotaUsageRepositoryMock.Setup(repository => repository.ReserveAsync(
                first.Id,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync((QuotaMutationResult?)null);
        _quotaUsageRepositoryMock.Setup(repository => repository.ReserveAsync(
                second.Id,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(MakeMutation(second));
        _quotaUsageRepositoryMock.Setup(repository => repository.TryInsertLogAsync(
                It.IsAny<UserSubscriptionLog>(),
                CancellationToken.None))
            .ReturnsAsync(true);

        var result = await ReserveAsync(idempotencyKey);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.SameAs(second));
        });
        _quotaUsageRepositoryMock.Verify(repository => repository.ReserveAsync(
            first.Id, _utcNow, CancellationToken.None), Times.Once);
        _quotaUsageRepositoryMock.Verify(repository => repository.ReserveAsync(
            second.Id, _utcNow, CancellationToken.None), Times.Once);
    }

    [TestCase(false, ServiceCreditErrorCode.ServiceCreditExhausted)]
    [TestCase(true, ServiceCreditErrorCode.QuotaMutationFailed)]
    public async Task ReserveAsync_AllCandidateMutationsFail_UsesRefreshedBalance(
        bool refreshedHasCredit,
        ServiceCreditErrorCode expectedError)
    {
        const string idempotencyKey = "reserve-all-mutations-fail";
        var candidate = MakeUsage(limitValue: 5, usedCount: 1, reservedCount: 0);
        var refreshed = MakeUsage(
            limitValue: 5,
            usedCount: refreshedHasCredit ? 3 : 4,
            reservedCount: 1);

        SetupIdempotency(idempotencyKey);
        SetupQuotaDefinition();
        _quotaUsageRepositoryMock.SetupSequence(repository => repository.GetEligibleByUserAsync(
                _userId,
                IServiceCreditService.QuotaCode,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(new[] { candidate })
            .ReturnsAsync(new[] { refreshed });
        _quotaUsageRepositoryMock.Setup(repository => repository.ReserveAsync(
                candidate.Id,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync((QuotaMutationResult?)null);

        var result = await ReserveAsync(idempotencyKey);

        Assert.That(result.Error, Is.EqualTo(expectedError));
        _quotaUsageRepositoryMock.Verify(repository => repository.GetEligibleByUserAsync(
            _userId, IServiceCreditService.QuotaCode, _utcNow, CancellationToken.None), Times.Exactly(2));
    }

    [Test]
    public async Task MutateAsync_ExistingLog_AcquiresLockBeforeLookupAndReturnsDuplicate()
    {
        const string idempotencyKey = "mutation-existing";
        var calls = new List<string>();
        _quotaUsageRepositoryMock.Setup(repository => repository.AcquireIdempotencyLockAsync(
                idempotencyKey,
                CancellationToken.None))
            .Callback(() => calls.Add("lock"))
            .Returns(Task.CompletedTask);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetLogByIdempotencyKeyAsync(
                idempotencyKey,
                CancellationToken.None))
            .Callback(() => calls.Add("log"))
            .ReturnsAsync(new UserSubscriptionLog());

        var result = await MutateAsync(SubscriptionQuotaActionType.Release, idempotencyKey: idempotencyKey);

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(new[] { "lock", "log" }));
            Assert.That(result, Is.EqualTo(QuotaMutationStatus.Duplicate));
        });
        _quotaUsageRepositoryMock.Verify(repository => repository.GetByIdForQuotaAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoMutationRepositoryCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MutateAsync_MissingOrMismatchedUsage_ReturnsRejected(bool quotaMismatch)
    {
        const string idempotencyKey = "mutation-invalid-usage";
        var usageId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var requestedQuotaId = Guid.NewGuid();
        SetupIdempotency(idempotencyKey);
        var usage = quotaMismatch
            ? MakeUsage(id: usageId, subscriptionId: subscriptionId, quotaId: Guid.NewGuid())
            : null;
        _quotaUsageRepositoryMock.Setup(repository => repository.GetByIdForQuotaAsync(
                usageId,
                subscriptionId,
                IServiceCreditService.QuotaCode,
                CancellationToken.None))
            .ReturnsAsync(usage);

        var result = await _service.MutateAsync(
            usageId,
            subscriptionId,
            requestedQuotaId,
            SubscriptionQuotaActionType.Release,
            "RecoveryPlanRequest",
            _referenceId,
            _actorUserId,
            idempotencyKey,
            "release",
            _utcNow,
            CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Rejected));
        VerifyNoMutationRepositoryCalls();
    }

    [TestCase(SubscriptionQuotaActionType.Release)]
    [TestCase(SubscriptionQuotaActionType.Consume)]
    [TestCase(SubscriptionQuotaActionType.Restore)]
    public async Task MutateAsync_SupportedMutationSucceeds_InsertsActionLogAndReturnsApplied(
        SubscriptionQuotaActionType actionType)
    {
        const string idempotencyKey = "mutation-success";
        var usage = MakeUsage();
        var mutation = MakeMutation(usage);
        UserSubscriptionLog? insertedLog = null;
        SetupMutationUsage(idempotencyKey, usage);
        SetupMutationResult(actionType, usage.Id, mutation);
        _quotaUsageRepositoryMock.Setup(repository => repository.TryInsertLogAsync(
                It.IsAny<UserSubscriptionLog>(),
                CancellationToken.None))
            .Callback<UserSubscriptionLog, CancellationToken>((log, _) => insertedLog = log)
            .ReturnsAsync(true);

        var result = await _service.MutateAsync(
            usage.Id,
            usage.UserSubscriptionId,
            usage.QuotaId,
            actionType,
            "RecoveryPlanRequest",
            _referenceId,
            _actorUserId,
            idempotencyKey,
            "mutation reason",
            _utcNow,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(QuotaMutationStatus.Applied));
            Assert.That(insertedLog, Is.Not.Null);
            Assert.That(insertedLog!.ActionType, Is.EqualTo(actionType));
            Assert.That(insertedLog.Quantity, Is.EqualTo(1));
            Assert.That(insertedLog.ReferenceType, Is.EqualTo("RecoveryPlanRequest"));
            Assert.That(insertedLog.ReferenceId, Is.EqualTo(_referenceId));
            Assert.That(insertedLog.IdempotencyKey, Is.EqualTo(idempotencyKey));
        });
        VerifyMutationCalled(actionType, usage.Id, Times.Once());
    }

    [Test]
    public async Task MutateAsync_RepositoryMutationReturnsNull_ReturnsRejectedWithoutLog()
    {
        const string idempotencyKey = "mutation-repository-rejected";
        var usage = MakeUsage();
        SetupMutationUsage(idempotencyKey, usage);
        _quotaUsageRepositoryMock.Setup(repository => repository.ReleaseAsync(
                usage.Id,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync((QuotaMutationResult?)null);

        var result = await MutateAsync(
            SubscriptionQuotaActionType.Release,
            usage,
            idempotencyKey);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Rejected));
        _quotaUsageRepositoryMock.Verify(repository => repository.TryInsertLogAsync(
            It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task MutateAsync_DuplicateLogInsert_ReturnsDuplicate()
    {
        const string idempotencyKey = "mutation-log-duplicate";
        var usage = MakeUsage();
        SetupMutationUsage(idempotencyKey, usage);
        _quotaUsageRepositoryMock.Setup(repository => repository.ReleaseAsync(
                usage.Id,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(MakeMutation(usage));
        _quotaUsageRepositoryMock.Setup(repository => repository.TryInsertLogAsync(
                It.IsAny<UserSubscriptionLog>(),
                CancellationToken.None))
            .ReturnsAsync(false);

        var result = await MutateAsync(
            SubscriptionQuotaActionType.Release,
            usage,
            idempotencyKey);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Duplicate));
    }

    [Test]
    public async Task GrantAsync_ExistingPaymentGrantLog_ReturnsDuplicate()
    {
        var paymentId = Guid.NewGuid();
        var expectedKey = $"credit:grant:payment:{paymentId:N}";
        _quotaUsageRepositoryMock.Setup(repository => repository.GetLogByIdempotencyKeyAsync(
                expectedKey,
                CancellationToken.None))
            .ReturnsAsync(new UserSubscriptionLog());

        var result = await _service.GrantAsync(
            Guid.NewGuid(), paymentId, _actorUserId, _utcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Duplicate));
        _quotaUsageRepositoryMock.Verify(repository => repository.GetBySubscriptionForQuotaAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GrantAsync_UsageDoesNotExist_ReturnsRejected()
    {
        var subscriptionId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        SetupMissingGrantLog(paymentId);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetBySubscriptionForQuotaAsync(
                subscriptionId,
                IServiceCreditService.QuotaCode,
                CancellationToken.None))
            .ReturnsAsync((UserSubscriptionUsage?)null);

        var result = await _service.GrantAsync(
            subscriptionId, paymentId, _actorUserId, _utcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Rejected));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task GrantAsync_NonPositiveLimit_ReturnsRejected(int limitValue)
    {
        var paymentId = Guid.NewGuid();
        var usage = MakeUsage(limitValue: limitValue);
        SetupMissingGrantLog(paymentId);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetBySubscriptionForQuotaAsync(
                usage.UserSubscriptionId,
                IServiceCreditService.QuotaCode,
                CancellationToken.None))
            .ReturnsAsync(usage);

        var result = await _service.GrantAsync(
            usage.UserSubscriptionId, paymentId, _actorUserId, _utcNow, CancellationToken.None);

        Assert.That(result, Is.EqualTo(QuotaMutationStatus.Rejected));
        _quotaUsageRepositoryMock.Verify(repository => repository.TryInsertLogAsync(
            It.IsAny<UserSubscriptionLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GrantAsync_ValidUsage_InsertsGrantLogAndReturnsApplied()
    {
        var paymentId = Guid.NewGuid();
        var usage = MakeUsage(limitValue: 12, usedCount: 3, reservedCount: 2);
        var expectedKey = $"credit:grant:payment:{paymentId:N}";
        UserSubscriptionLog? insertedLog = null;
        SetupMissingGrantLog(paymentId);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetBySubscriptionForQuotaAsync(
                usage.UserSubscriptionId,
                IServiceCreditService.QuotaCode,
                CancellationToken.None))
            .ReturnsAsync(usage);
        _quotaUsageRepositoryMock.Setup(repository => repository.TryInsertLogAsync(
                It.IsAny<UserSubscriptionLog>(),
                CancellationToken.None))
            .Callback<UserSubscriptionLog, CancellationToken>((log, _) => insertedLog = log)
            .ReturnsAsync(true);

        var result = await _service.GrantAsync(
            usage.UserSubscriptionId, paymentId, _actorUserId, _utcNow, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(QuotaMutationStatus.Applied));
            Assert.That(insertedLog, Is.Not.Null);
            Assert.That(insertedLog!.ActionType, Is.EqualTo(SubscriptionQuotaActionType.Grant));
            Assert.That(insertedLog.ReferenceType, Is.EqualTo("Payment"));
            Assert.That(insertedLog.ReferenceId, Is.EqualTo(paymentId));
            Assert.That(insertedLog.Quantity, Is.EqualTo(usage.LimitValue));
            Assert.That(insertedLog.PerformedByUserId, Is.EqualTo(_actorUserId));
            Assert.That(insertedLog.IdempotencyKey, Is.EqualTo(expectedKey));
            Assert.That(insertedLog.UserSubscriptionUsageId, Is.EqualTo(usage.Id));
            Assert.That(insertedLog.UserSubscriptionId, Is.EqualTo(usage.UserSubscriptionId));
            Assert.That(insertedLog.QuotaId, Is.EqualTo(usage.QuotaId));
        });
    }

    private Task<ServiceCreditOperationResult<UserSubscriptionUsage>> ReserveAsync(string idempotencyKey) =>
        _service.ReserveAsync(
            _userId,
            "RecoveryPlanRequest",
            _referenceId,
            _actorUserId,
            idempotencyKey,
            "reserve reason",
            _utcNow,
            CancellationToken.None);

    private Task<QuotaMutationStatus> MutateAsync(
        SubscriptionQuotaActionType actionType,
        UserSubscriptionUsage? usage = null,
        string idempotencyKey = "mutation-key")
    {
        usage ??= MakeUsage();
        return _service.MutateAsync(
            usage.Id,
            usage.UserSubscriptionId,
            usage.QuotaId,
            actionType,
            "RecoveryPlanRequest",
            _referenceId,
            _actorUserId,
            idempotencyKey,
            "mutation reason",
            _utcNow,
            CancellationToken.None);
    }

    private void SetupIdempotency(string idempotencyKey, UserSubscriptionLog? existingLog = null)
    {
        _quotaUsageRepositoryMock.Setup(repository => repository.AcquireIdempotencyLockAsync(
                idempotencyKey,
                CancellationToken.None))
            .Returns(Task.CompletedTask);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetLogByIdempotencyKeyAsync(
                idempotencyKey,
                CancellationToken.None))
            .ReturnsAsync(existingLog);
    }

    private void SetupQuotaDefinition(bool isActive = true)
    {
        _subscriptionPlanQuotaRepositoryMock.Setup(repository => repository.GetQuotaDefinitionByCodeAsync(
                IServiceCreditService.QuotaCode,
                CancellationToken.None))
            .ReturnsAsync(new Quota
            {
                Id = Guid.NewGuid(),
                Code = IServiceCreditService.QuotaCode,
                IsActive = isActive
            });
    }

    private void SetupReserveCandidates(
        string idempotencyKey,
        IReadOnlyList<UserSubscriptionUsage> candidates)
    {
        SetupIdempotency(idempotencyKey);
        SetupQuotaDefinition();
        _quotaUsageRepositoryMock.Setup(repository => repository.GetEligibleByUserAsync(
                _userId,
                IServiceCreditService.QuotaCode,
                _utcNow,
                CancellationToken.None))
            .ReturnsAsync(candidates);
    }

    private void SetupMutationUsage(string idempotencyKey, UserSubscriptionUsage usage)
    {
        SetupIdempotency(idempotencyKey);
        _quotaUsageRepositoryMock.Setup(repository => repository.GetByIdForQuotaAsync(
                usage.Id,
                usage.UserSubscriptionId,
                IServiceCreditService.QuotaCode,
                CancellationToken.None))
            .ReturnsAsync(usage);
    }

    private void SetupMutationResult(
        SubscriptionQuotaActionType actionType,
        Guid usageId,
        QuotaMutationResult mutation)
    {
        switch (actionType)
        {
            case SubscriptionQuotaActionType.Release:
                _quotaUsageRepositoryMock.Setup(repository => repository.ReleaseAsync(
                        usageId, _utcNow, CancellationToken.None))
                    .ReturnsAsync(mutation);
                break;
            case SubscriptionQuotaActionType.Consume:
                _quotaUsageRepositoryMock.Setup(repository => repository.ConsumeAsync(
                        usageId, _utcNow, CancellationToken.None))
                    .ReturnsAsync(mutation);
                break;
            case SubscriptionQuotaActionType.Restore:
                _quotaUsageRepositoryMock.Setup(repository => repository.RestoreAsync(
                        usageId, _utcNow, CancellationToken.None))
                    .ReturnsAsync(mutation);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(actionType));
        }
    }

    private void SetupMissingGrantLog(Guid paymentId)
    {
        var key = $"credit:grant:payment:{paymentId:N}";
        _quotaUsageRepositoryMock.Setup(repository => repository.GetLogByIdempotencyKeyAsync(
                key,
                CancellationToken.None))
            .ReturnsAsync((UserSubscriptionLog?)null);
    }

    private void VerifyMutationCalled(
        SubscriptionQuotaActionType actionType,
        Guid usageId,
        Times times)
    {
        switch (actionType)
        {
            case SubscriptionQuotaActionType.Release:
                _quotaUsageRepositoryMock.Verify(repository => repository.ReleaseAsync(
                    usageId, _utcNow, CancellationToken.None), times);
                break;
            case SubscriptionQuotaActionType.Consume:
                _quotaUsageRepositoryMock.Verify(repository => repository.ConsumeAsync(
                    usageId, _utcNow, CancellationToken.None), times);
                break;
            case SubscriptionQuotaActionType.Restore:
                _quotaUsageRepositoryMock.Verify(repository => repository.RestoreAsync(
                    usageId, _utcNow, CancellationToken.None), times);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(actionType));
        }
    }

    private void VerifyNoMutationRepositoryCalls()
    {
        _quotaUsageRepositoryMock.Verify(repository => repository.ReleaseAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _quotaUsageRepositoryMock.Verify(repository => repository.ConsumeAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _quotaUsageRepositoryMock.Verify(repository => repository.RestoreAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UserSubscriptionUsage MakeUsage(
        Guid? id = null,
        Guid? subscriptionId = null,
        Guid? quotaId = null,
        int limitValue = 10,
        int usedCount = 2,
        int reservedCount = 1) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            UserSubscriptionId = subscriptionId ?? Guid.NewGuid(),
            QuotaId = quotaId ?? Guid.NewGuid(),
            LimitValue = limitValue,
            UsedCount = usedCount,
            ReservedCount = reservedCount
        };

    private static QuotaMutationResult MakeMutation(
        UserSubscriptionUsage usage,
        int? usedAfter = null,
        int? reservedAfter = null) =>
        new(
            usage.Id,
            usage.UserSubscriptionId,
            usage.QuotaId,
            usage.LimitValue,
            usage.UsedCount,
            usedAfter ?? usage.UsedCount,
            usage.ReservedCount,
            reservedAfter ?? usage.ReservedCount);
}

using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.ServiceCredits;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanQuotaServiceTests
{
    private Mock<IServiceCreditService> _serviceCreditServiceMock = null!;
    private RecoveryPlanQuotaService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceCreditServiceMock = new Mock<IServiceCreditService>();
        _service = new RecoveryPlanQuotaService(_serviceCreditServiceMock.Object);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReserveUsageAsync_Success_ReturnsUsageAndPreservesReplayFlag(bool isReplay)
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var usage = new UserSubscriptionUsage
        {
            Id = Guid.NewGuid(),
            UserSubscriptionId = Guid.NewGuid(),
            QuotaId = Guid.NewGuid()
        };
        var idempotencyKey = "reserve-key";
        var utcNow = new DateTime(2026, 8, 12, 10, 30, 0, DateTimeKind.Utc);
        using var cancellationSource = new CancellationTokenSource();

        _serviceCreditServiceMock.Setup(service => service.ReserveAsync(
                userId,
                RecoveryPlanQuotaService.ReferenceType,
                requestId,
                actorUserId,
                idempotencyKey,
                "Recovery plan service credit reserved.",
                utcNow,
                cancellationSource.Token))
            .ReturnsAsync(ServiceCreditOperationResult<UserSubscriptionUsage>.Ok(usage, isReplay));

        var result = await _service.ReserveUsageAsync(
            userId,
            requestId,
            actorUserId,
            idempotencyKey,
            utcNow,
            cancellationSource.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.SameAs(usage));
            Assert.That(result.IsReplay, Is.EqualTo(isReplay));
            Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.None));
        });
        _serviceCreditServiceMock.VerifyAll();
    }

    [TestCase(ServiceCreditErrorCode.NoCreditPackage, RecoveryPlanErrorCode.NoCreditPackage)]
    [TestCase(ServiceCreditErrorCode.ServiceCreditNotConfigured, RecoveryPlanErrorCode.ServiceCreditNotConfigured)]
    [TestCase(ServiceCreditErrorCode.ServiceCreditExhausted, RecoveryPlanErrorCode.ServiceCreditExhausted)]
    [TestCase(ServiceCreditErrorCode.QuotaMutationFailed, RecoveryPlanErrorCode.QuotaMutationFailed)]
    public async Task ReserveUsageAsync_Failure_MapsServiceCreditError(
        ServiceCreditErrorCode serviceCreditError,
        RecoveryPlanErrorCode expectedError)
    {
        _serviceCreditServiceMock.Setup(service => service.ReserveAsync(
                It.IsAny<Guid>(),
                RecoveryPlanQuotaService.ReferenceType,
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceCreditOperationResult<UserSubscriptionUsage>.Fail(serviceCreditError));

        var result = await _service.ReserveUsageAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "reserve-key",
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Data, Is.Null);
            Assert.That(result.Error, Is.EqualTo(expectedError));
        });
    }

    [TestCase(nameof(RecoveryPlanQuotaService.ReleaseAsync), SubscriptionQuotaActionType.Release, QuotaMutationStatus.Applied)]
    [TestCase(nameof(RecoveryPlanQuotaService.ReleaseAsync), SubscriptionQuotaActionType.Release, QuotaMutationStatus.Duplicate)]
    [TestCase(nameof(RecoveryPlanQuotaService.ReleaseAsync), SubscriptionQuotaActionType.Release, QuotaMutationStatus.Rejected)]
    [TestCase(nameof(RecoveryPlanQuotaService.ConsumeAsync), SubscriptionQuotaActionType.Consume, QuotaMutationStatus.Applied)]
    [TestCase(nameof(RecoveryPlanQuotaService.ConsumeAsync), SubscriptionQuotaActionType.Consume, QuotaMutationStatus.Duplicate)]
    [TestCase(nameof(RecoveryPlanQuotaService.ConsumeAsync), SubscriptionQuotaActionType.Consume, QuotaMutationStatus.Rejected)]
    [TestCase(nameof(RecoveryPlanQuotaService.RestoreAsync), SubscriptionQuotaActionType.Restore, QuotaMutationStatus.Applied)]
    [TestCase(nameof(RecoveryPlanQuotaService.RestoreAsync), SubscriptionQuotaActionType.Restore, QuotaMutationStatus.Duplicate)]
    [TestCase(nameof(RecoveryPlanQuotaService.RestoreAsync), SubscriptionQuotaActionType.Restore, QuotaMutationStatus.Rejected)]
    public async Task MutationAdapter_DelegatesCorrectActionAndPassesThroughStatus(
        string methodName,
        SubscriptionQuotaActionType expectedAction,
        QuotaMutationStatus expectedStatus)
    {
        var usageId = Guid.NewGuid();
        var userSubscriptionId = Guid.NewGuid();
        var quotaId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var idempotencyKey = $"{methodName}-key";
        var utcNow = new DateTime(2026, 8, 12, 11, 0, 0, DateTimeKind.Utc);
        using var cancellationSource = new CancellationTokenSource();

        _serviceCreditServiceMock.Setup(service => service.MutateAsync(
                usageId,
                userSubscriptionId,
                quotaId,
                expectedAction,
                RecoveryPlanQuotaService.ReferenceType,
                requestId,
                actorUserId,
                idempotencyKey,
                It.IsAny<string>(),
                utcNow,
                cancellationSource.Token))
            .ReturnsAsync(expectedStatus);

        var result = methodName switch
        {
            nameof(RecoveryPlanQuotaService.ReleaseAsync) => await _service.ReleaseAsync(
                usageId, userSubscriptionId, quotaId, requestId, actorUserId,
                idempotencyKey, utcNow, cancellationSource.Token),
            nameof(RecoveryPlanQuotaService.ConsumeAsync) => await _service.ConsumeAsync(
                usageId, userSubscriptionId, quotaId, requestId, actorUserId,
                idempotencyKey, utcNow, cancellationSource.Token),
            nameof(RecoveryPlanQuotaService.RestoreAsync) => await _service.RestoreAsync(
                usageId, userSubscriptionId, quotaId, requestId, actorUserId,
                idempotencyKey, utcNow, cancellationSource.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName))
        };

        Assert.That(result, Is.EqualTo(expectedStatus));
        _serviceCreditServiceMock.VerifyAll();
    }
}

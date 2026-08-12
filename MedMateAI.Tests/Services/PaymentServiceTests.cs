using System.Globalization;
using System.Security.Claims;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class PaymentServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IPaymentTransactionRepository> _transactionRepoMock = null!;
    private Mock<IPayOSService> _payOsMock = null!;
    private Mock<IServiceCreditService> _serviceCreditServiceMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private Mock<ILogger<PaymentService>> _loggerMock = null!;
    private PaymentService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _subId = Guid.NewGuid();
    private readonly Guid _paymentId = Guid.NewGuid();
    private readonly Guid _transactionId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionRepoMock = new Mock<IPaymentTransactionRepository>();
        _payOsMock = new Mock<IPayOSService>();
        _serviceCreditServiceMock = new Mock<IServiceCreditService>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _loggerMock = new Mock<ILogger<PaymentService>>();

        _unitOfWorkMock.Setup(u => u.PaymentTransactions).Returns(_transactionRepoMock.Object);

        // Transaction setups
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _serviceCreditServiceMock.Setup(service => service.GrantAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Applied);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContextAccessorMock.Setup(accessor => accessor.HttpContext)
            .Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });

        _service = new PaymentService(
            _unitOfWorkMock.Object,
            _payOsMock.Object,
            _serviceCreditServiceMock.Object,
            _httpContextAccessorMock.Object,
            _loggerMock.Object);
    }

    // ── ProcessPayOSReturnAsync & ProcessPayOSCancelAsync ────────────────────

    [Test]
    [Category("A")]
    public async Task ProcessPayOSReturnAsync_InvalidOrderCode_ReturnsSuccessFalse()
    {
        // Arrange
        var query = new Dictionary<string, string> { { "orderCode", "invalid" } };

        // Act
        var result = await _service.ProcessPayOSReturnAsync(query);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Invalid orderCode."));
    }

    [Test]
    [Category("A")]
    public async Task ProcessPayOSReturnAsync_TransactionNotFound_ReturnsSuccessFalse()
    {
        // Arrange
        var query = new Dictionary<string, string> { { "orderCode", "123456" } };
        _transactionRepoMock.Setup(r => r.GetByTransactionReferenceAsync("123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        // Act
        var result = await _service.ProcessPayOSReturnAsync(query);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Payment transaction not found."));
    }

    [Test]
    [Category("N")]
    public async Task ProcessPayOSReturnAsync_ValidPaidTransaction_ReturnsSuccessTrue()
    {
        // Arrange
        var query = new Dictionary<string, string> { { "orderCode", "123456" } };
        var transaction = new PaymentTransaction
        {
            Id = _transactionId,
            UserSubscriptionId = _subId,
            PaymentId = _paymentId,
            Status = "PAID",
            Payment = new Payment
            {
                Id = _paymentId,
                Status = PaymentStatus.Paid,
                UserSubscription = new UserSubscription
                {
                    Id = _subId,
                    Status = SubscriptionStatus.Active
                }
            }
        };

        _transactionRepoMock.Setup(r => r.GetByTransactionReferenceAsync("123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _service.ProcessPayOSReturnAsync(query);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.OrderCode, Is.EqualTo("123456"));
    }

    // ── ProcessPayOSWebhookAsync ─────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task ProcessPayOSWebhookAsync_InvalidWebhookSignature_ReturnsFalse()
    {
        // Arrange
        var rawBody = "invalid signature body";
        var verifyResult = new PayOSWebhookResult { IsValid = false };
        _payOsMock.Setup(p => p.VerifyWebhookAsync(rawBody, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifyResult);

        // Act
        var result = await _service.ProcessPayOSWebhookAsync(rawBody);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    [Category("N")]
    public async Task ProcessPayOSWebhookAsync_ValidButNotPaidOrCancelled_ReturnsTrue()
    {
        // Arrange
        var rawBody = "valid body but pending";
        var verifyResult = new PayOSWebhookResult { IsValid = true, IsPaid = false, IsCancelled = false };
        _payOsMock.Setup(p => p.VerifyWebhookAsync(rawBody, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifyResult);

        // Act
        var result = await _service.ProcessPayOSWebhookAsync(rawBody);

        // Assert
        Assert.That(result, Is.True);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task ProcessPayOSWebhookAsync_PaidValidCallback_MutatesAndCommits()
    {
        // Arrange
        var rawBody = "valid body paid";
        var verifyResult = new PayOSWebhookResult
        {
            IsValid = true,
            IsPaid = true,
            IsCancelled = false,
            OrderCode = 123456,
            Amount = 100000,
            PaymentLinkId = "link_abc"
        };
        _payOsMock.Setup(p => p.VerifyWebhookAsync(rawBody, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifyResult);

        var transaction = MakePendingTransaction(123456);
        var payment = transaction.Payment!;
        var subscription = payment.UserSubscription;

        _unitOfWorkMock.Setup(u => u.ClearTrackedChanges());
        _transactionRepoMock.Setup(r => r.GetByTransactionReferenceForUpdateAsync("123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _serviceCreditServiceMock.Setup(service => service.GrantAsync(
                _subId,
                _paymentId,
                _userId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Applied);

        // Act
        var result = await _service.ProcessPayOSWebhookAsync(rawBody);

        // Assert
        Assert.That(result, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Paid));
            Assert.That(payment.PaidAt, Is.Not.Null);
            Assert.That(subscription.Status, Is.EqualTo(SubscriptionStatus.Active));
            Assert.That(subscription.StartDate, Is.Not.Null);
            Assert.That(subscription.EndDate, Is.Null);
            Assert.That(subscription.AutoRenew, Is.False);
        });
        _serviceCreditServiceMock.Verify(service => service.GrantAsync(
            _subId,
            _paymentId,
            _userId,
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessPayOSWebhookAsync_PaidWithDuplicateGrant_IsIdempotentAndCommits()
    {
        const string rawBody = "paid duplicate grant";
        var transaction = MakePendingTransaction(123456);
        _payOsMock.Setup(service => service.VerifyWebhookAsync(rawBody, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWebhookResult(isPaid: true));
        _transactionRepoMock.Setup(repository => repository.GetByTransactionReferenceForUpdateAsync(
                "123456",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _serviceCreditServiceMock.Setup(service => service.GrantAsync(
                _subId,
                _paymentId,
                _userId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Duplicate);

        var result = await _service.ProcessPayOSWebhookAsync(rawBody);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(transaction.Payment!.Status, Is.EqualTo(PaymentStatus.Paid));
            Assert.That(transaction.Payment.UserSubscription.Status, Is.EqualTo(SubscriptionStatus.Active));
        });
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessPayOSWebhookAsync_PaidWithRejectedGrant_ReturnsFalseAndRollsBack()
    {
        const string rawBody = "paid rejected grant";
        var transaction = MakePendingTransaction(123456);
        _payOsMock.Setup(service => service.VerifyWebhookAsync(rawBody, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWebhookResult(isPaid: true));
        _transactionRepoMock.Setup(repository => repository.GetByTransactionReferenceForUpdateAsync(
                "123456",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _serviceCreditServiceMock.Setup(service => service.GrantAsync(
                _subId,
                _paymentId,
                _userId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaMutationStatus.Rejected);

        var result = await _service.ProcessPayOSWebhookAsync(rawBody);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(transaction.Payment!.Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(transaction.Payment.UserSubscription.Status, Is.EqualTo(SubscriptionStatus.Pending));
        });
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(
            CancellationToken.None), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessPayOSWebhookAsync_Cancelled_DoesNotGrantCredits()
    {
        const string rawBody = "cancelled payment";
        var transaction = MakePendingTransaction(123456);
        _payOsMock.Setup(service => service.VerifyWebhookAsync(rawBody, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeWebhookResult(isPaid: false, isCancelled: true));
        _transactionRepoMock.Setup(repository => repository.GetByTransactionReferenceForUpdateAsync(
                "123456",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var result = await _service.ProcessPayOSWebhookAsync(rawBody);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(transaction.Payment!.Status, Is.EqualTo(PaymentStatus.Cancelled));
            Assert.That(transaction.Payment.UserSubscription.Status, Is.EqualTo(SubscriptionStatus.Cancelled));
        });
        _serviceCreditServiceMock.Verify(service => service.GrantAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("FAILED")]
    [TestCase("EXPIRED")]
    public async Task ReconcilePayOSPaymentAsync_FailedOrExpired_DoesNotGrantCredits(string providerStatus)
    {
        const long orderCode = 123456;
        var transaction = MakePendingTransaction(orderCode);
        _transactionRepoMock.Setup(repository => repository.GetByTransactionReferenceAsync(
                "123456",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _payOsMock.Setup(service => service.GetPaymentLinkAsync(
                orderCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayOSPaymentLinkLookupResult.Ok(new PayOSPaymentLinkResult
            {
                OrderCode = orderCode,
                PaymentLinkId = "link_abc",
                Amount = 100000,
                AmountPaid = 0,
                AmountRemaining = 100000,
                Status = providerStatus
            }));
        _transactionRepoMock.Setup(repository => repository.GetByTransactionReferenceForUpdateAsync(
                "123456",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var result = await _service.ReconcilePayOSPaymentAsync(orderCode);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(transaction.Payment!.Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(transaction.Payment.UserSubscription.Status, Is.EqualTo(SubscriptionStatus.Cancelled));
        });
        _serviceCreditServiceMock.Verify(service => service.GrantAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── GetPayOSPaymentStatusAsync ───────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetPayOSPaymentStatusAsync_InvalidOrderCode_ReturnsNull()
    {
        Assert.That(await _service.GetPayOSPaymentStatusAsync(0), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetPayOSPaymentStatusAsync_TransactionNotFound_ReturnsNull()
    {
        _transactionRepoMock.Setup(r => r.GetByTransactionReferenceAsync("123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        Assert.That(await _service.GetPayOSPaymentStatusAsync(123456), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetPayOSPaymentStatusAsync_TransactionExists_ReturnsStatus()
    {
        // Arrange
        var transaction = new PaymentTransaction
        {
            Id = _transactionId,
            UserSubscriptionId = _subId,
            PaymentId = _paymentId,
            Status = "PAID",
            Payment = new Payment
            {
                Id = _paymentId,
                Status = PaymentStatus.Paid,
                UserSubscription = new UserSubscription
                {
                    Id = _subId,
                    Status = SubscriptionStatus.Active
                }
            }
        };
        _transactionRepoMock.Setup(r => r.GetByTransactionReferenceAsync("123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _service.GetPayOSPaymentStatusAsync(123456);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsPaid, Is.True);
    }

    private PaymentTransaction MakePendingTransaction(long orderCode)
    {
        var subscription = new UserSubscription
        {
            Id = _subId,
            UserId = _userId,
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Pending,
            StartDate = null,
            EndDate = null,
            AutoRenew = true,
            Plan = new SubscriptionPlan { Id = Guid.NewGuid(), IsDeleted = false },
            IsDeleted = false
        };
        var payment = new Payment
        {
            Id = _paymentId,
            UserId = _userId,
            UserSubscriptionId = _subId,
            Status = PaymentStatus.Pending,
            Amount = 100000m,
            UserSubscription = subscription,
            IsDeleted = false
        };
        return new PaymentTransaction
        {
            Id = _transactionId,
            UserId = _userId,
            UserSubscriptionId = _subId,
            PaymentId = _paymentId,
            Amount = 100000m,
            Status = "Pending",
            PaymentProvider = "payOS",
            TransactionReference = orderCode.ToString(CultureInfo.InvariantCulture),
            Payment = payment,
            UserSubscription = subscription,
            IsDeleted = false
        };
    }

    private static PayOSWebhookResult MakeWebhookResult(bool isPaid, bool isCancelled = false) => new()
    {
        IsValid = true,
        IsPaid = isPaid,
        IsCancelled = isCancelled,
        OrderCode = 123456,
        Amount = 100000,
        PaymentLinkId = "link_abc"
    };
}

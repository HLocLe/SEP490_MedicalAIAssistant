using System.Globalization;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.IService;
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

        _service = new PaymentService(
            _unitOfWorkMock.Object,
            _payOsMock.Object,
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

        var plan = new SubscriptionPlan { Id = Guid.NewGuid(), IsDeleted = false };
        var subscription = new UserSubscription
        {
            Id = _subId,
            UserId = _userId,
            Status = SubscriptionStatus.Pending,
            Plan = plan,
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
        var transaction = new PaymentTransaction
        {
            Id = _transactionId,
            UserId = _userId,
            UserSubscriptionId = _subId,
            PaymentId = _paymentId,
            Amount = 100000m,
            Status = "Pending",
            PaymentProvider = "payOS",
            TransactionReference = "123456",
            Payment = payment,
            IsDeleted = false
        };

        _unitOfWorkMock.Setup(u => u.ClearTrackedChanges());
        _transactionRepoMock.Setup(r => r.GetByTransactionReferenceForUpdateAsync("123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _service.ProcessPayOSWebhookAsync(rawBody);

        // Assert
        Assert.That(result, Is.True);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
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
}

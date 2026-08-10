using System.Linq.Expressions;
using System.Security.Claims;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.DTOs.UserSubscriptions.Requests;
using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.AspNetCore.Http;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class UserSubscriptionServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IUserSubscriptionRepository> _subscriptionsMock = null!;
    private Mock<IGenericRepository<SubscriptionPlan>> _plansMock = null!;
    private Mock<IPaymentRepository> _paymentsMock = null!;
    private Mock<IPaymentTransactionRepository> _transactionsMock = null!;
    private Mock<IPayOSService> _payOsMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private UserSubscriptionService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _subscriptionsMock = new Mock<IUserSubscriptionRepository>();
        _plansMock = new Mock<IGenericRepository<SubscriptionPlan>>();
        _paymentsMock = new Mock<IPaymentRepository>();
        _transactionsMock = new Mock<IPaymentTransactionRepository>();
        _payOsMock = new Mock<IPayOSService>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        _unitOfWorkMock.Setup(u => u.UserSubscriptions).Returns(_subscriptionsMock.Object);
        _unitOfWorkMock.Setup(u => u.SubscriptionPlans).Returns(_plansMock.Object);
        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentsMock.Object);
        _unitOfWorkMock.Setup(u => u.PaymentTransactions).Returns(_transactionsMock.Object);

        // Transaction setups
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Default mock authenticated HttpContext
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = claimsPrincipal };
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        _service = new UserSubscriptionService(
            _unitOfWorkMock.Object,
            _payOsMock.Object,
            _httpContextAccessorMock.Object);
    }

    private void MockUnauthenticated()
    {
        var httpContext = new DefaultHttpContext(); // No user identity
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);
    }

    // â”€â”€ CheckoutAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task CheckoutAsync_NullRequest_ReturnsError()
    {
        // Act
        var result = await _service.CheckoutAsync(null!);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("Request body is required."));
    }

    [Test]
    [Category("B")]
    public async Task CheckoutAsync_EmptyPlanId_ReturnsError()
    {
        // Arrange
        var req = new CheckoutSubscriptionRequest { PlanId = Guid.Empty };

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("PlanId is required."));
    }

    [Test]
    [Category("A")]
    public async Task CheckoutAsync_Unauthenticated_ReturnsError()
    {
        // Arrange
        MockUnauthenticated();
        var req = new CheckoutSubscriptionRequest { PlanId = Guid.NewGuid() };

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("User is not authenticated."));
    }

    [Test]
    [Category("A")]
    public async Task CheckoutAsync_PlanNotFound_ReturnsError()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var req = new CheckoutSubscriptionRequest { PlanId = planId };
        _plansMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("Subscription plan not found."));
    }

    [Test]
    [Category("A")]
    public async Task CheckoutAsync_PlanNotActive_ReturnsError()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan { Id = planId, IsActive = false, IsDeleted = false };
        var req = new CheckoutSubscriptionRequest { PlanId = planId };
        _plansMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("Subscription plan is not active."));
    }

    [Test]
    [Category("B")]
    public async Task CheckoutAsync_PlanPriceZeroOrLess_ReturnsError()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan { Id = planId, IsActive = true, IsDeleted = false, Price = 0 };
        var req = new CheckoutSubscriptionRequest { PlanId = planId };
        _plansMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("This plan does not require payOS payment."));
    }

    [Test]
    [Category("A")]
    public async Task CheckoutAsync_AlreadyHasActiveSubscription_ReturnsError()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan { Id = planId, IsActive = true, IsDeleted = false, Price = 100000 };
        var req = new CheckoutSubscriptionRequest { PlanId = planId };
        _plansMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var activeSub = new UserSubscription { Id = Guid.NewGuid(), UserId = _userId };
        _subscriptionsMock.Setup(r => r.GetCurrentActiveByUserAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSub);

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("You already have an active subscription."));
    }

    [Test]
    [Category("A")]
    public async Task CheckoutAsync_PayOSExceptionOccurs_CancelsSubscriptionAndReturnsError()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan { Id = planId, IsActive = true, IsDeleted = false, Price = 100000, PlanName = "Gold" };
        var req = new CheckoutSubscriptionRequest { PlanId = planId, AutoRenew = true };
        _plansMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _subscriptionsMock.Setup(r => r.GetCurrentActiveByUserAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        // Mock OrderCode duplicate to check retry logic
        _transactionsMock.SetupSequence(r => r.GetByTransactionReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        _payOsMock.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSCreatePaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("PayOS connection failed"));

        UserSubscription? savedSub = null;
        _subscriptionsMock.Setup(r => r.Add(It.IsAny<UserSubscription>()))
            .Callback<UserSubscription>(s => savedSub = s);

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("Create payOS payment link failed."));
        Assert.That(savedSub, Is.Not.Null);
        Assert.That(savedSub.Status, Is.EqualTo(SubscriptionStatus.Cancelled)); // Verify status updated to Cancelled

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task CheckoutAsync_ValidRequest_Success()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan { Id = planId, IsActive = true, IsDeleted = false, Price = 100000, PlanName = "Gold" };
        var req = new CheckoutSubscriptionRequest { PlanId = planId, AutoRenew = true };
        _plansMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _subscriptionsMock.Setup(r => r.GetCurrentActiveByUserAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        _transactionsMock.Setup(r => r.GetByTransactionReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        var payOsResult = new PayOSCreatePaymentResult
        {
            PaymentLinkId = "link_123",
            Status = "PENDING",
            CheckoutUrl = "checkout_url_abc",
            RawResponse = "raw_response_text"
        };
        _payOsMock.Setup(p => p.CreatePaymentLinkAsync(It.IsAny<PayOSCreatePaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payOsResult);

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.PaymentUrl, Is.EqualTo("checkout_url_abc"));
        
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ GetMySubscriptionsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task GetMySubscriptionsAsync_Unauthenticated_ReturnsEmpty()
    {
        // Arrange
        MockUnauthenticated();

        // Act
        var result = await _service.GetMySubscriptionsAsync();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    [Category("N")]
    public async Task GetMySubscriptionsAsync_Authenticated_ReturnsMappedSubscriptions()
    {
        // Arrange
        var subs = new List<UserSubscription>
        {
            new() { Id = Guid.NewGuid(), UserId = _userId, Plan = new SubscriptionPlan { PlanName = "Premium", Price = 250000 } }
        };

        _subscriptionsMock.Setup(r => r.GetByUserWithPlanAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subs);

        // Act
        var result = await _service.GetMySubscriptionsAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PlanName, Is.EqualTo("Premium"));
        Assert.That(result[0].Price, Is.EqualTo(250000));
    }

    // â”€â”€ GetByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetByIdAsync_EmptyId_ReturnsNull()
    {
        Assert.That(await _service.GetByIdAsync(Guid.Empty), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetByIdAsync_SubscriptionNotFoundOrDeleted_ReturnsNull()
    {
        // Arrange
        var subId = Guid.NewGuid();
        _subscriptionsMock.Setup(r => r.GetByIdWithPlanAsync(subId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        // Act & Assert
        Assert.That(await _service.GetByIdAsync(subId), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetByIdAsync_SubscriptionExists_ReturnsMapped()
    {
        // Arrange
        var subId = Guid.NewGuid();
        var sub = new UserSubscription { Id = subId, Plan = new SubscriptionPlan { PlanName = "Gold" } };
        _subscriptionsMock.Setup(r => r.GetByIdWithPlanAsync(subId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sub);

        // Act
        var result = await _service.GetByIdAsync(subId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PlanName, Is.EqualTo("Gold"));
    }

    // â”€â”€ CancelAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task CancelAsync_EmptyId_ReturnsError()
    {
        // Act
        var result = await _service.CancelAsync(Guid.Empty);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("Invalid subscription id."));
    }

    [Test]
    [Category("A")]
    public async Task CancelAsync_Unauthenticated_ReturnsError()
    {
        // Arrange
        MockUnauthenticated();

        // Act
        var result = await _service.CancelAsync(Guid.NewGuid());

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("User is not authenticated."));
    }

    [Test]
    [Category("A")]
    public async Task CancelAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        var subId = Guid.NewGuid();
        _subscriptionsMock.Setup(r => r.GetByIdWithPlanAsync(subId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        // Act
        var result = await _service.CancelAsync(subId);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task CancelAsync_UserIdMismatch_ReturnsNotFound()
    {
        // Arrange
        var subId = Guid.NewGuid();
        var sub = new UserSubscription { Id = subId, UserId = Guid.NewGuid() }; // Mismatch
        _subscriptionsMock.Setup(r => r.GetByIdWithPlanAsync(subId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sub);

        // Act
        var result = await _service.CancelAsync(subId);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task CancelAsync_ActiveSubscription_CancelsAndReturnsUpdated()
    {
        // Arrange
        var subId = Guid.NewGuid();
        var sub = new UserSubscription { Id = subId, UserId = _userId, Status = SubscriptionStatus.Active, AutoRenew = true };
        var updatedSub = new UserSubscription { Id = subId, UserId = _userId, Status = SubscriptionStatus.Cancelled, AutoRenew = false };

        _subscriptionsMock.SetupSequence(r => r.GetByIdWithPlanAsync(subId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sub)
            .ReturnsAsync(updatedSub);

        // Act
        var result = await _service.CancelAsync(subId);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.Status, Is.EqualTo(SubscriptionStatus.Cancelled));
        Assert.That(result.Data.AutoRenew, Is.False);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

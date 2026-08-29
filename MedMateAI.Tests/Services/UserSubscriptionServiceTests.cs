using System.Linq.Expressions;
using System.Security.Claims;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.DTOs.Payments.Responses;
using MedMateAI.Application.DTOs.UserSubscriptions.Requests;
using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Payments;
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
    private Mock<IQuotaUsageRepository> _quotaUsageRepositoryMock = null!;
    private Mock<ISubscriptionPlanQuotaRepository> _subscriptionPlanQuotaRepositoryMock = null!;
    private Mock<IPayOSService> _payOsMock = null!;
    private Mock<IPaymentService> _paymentServiceMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private UserSubscriptionService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _serviceCreditQuotaId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _subscriptionsMock = new Mock<IUserSubscriptionRepository>();
        _plansMock = new Mock<IGenericRepository<SubscriptionPlan>>();
        _paymentsMock = new Mock<IPaymentRepository>();
        _transactionsMock = new Mock<IPaymentTransactionRepository>();
        _quotaUsageRepositoryMock = new Mock<IQuotaUsageRepository>();
        _subscriptionPlanQuotaRepositoryMock = new Mock<ISubscriptionPlanQuotaRepository>();
        _payOsMock = new Mock<IPayOSService>();
        _paymentServiceMock = new Mock<IPaymentService>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        _unitOfWorkMock.Setup(u => u.UserSubscriptions).Returns(_subscriptionsMock.Object);
        _unitOfWorkMock.Setup(u => u.SubscriptionPlans).Returns(_plansMock.Object);
        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentsMock.Object);
        _unitOfWorkMock.Setup(u => u.PaymentTransactions).Returns(_transactionsMock.Object);
        _unitOfWorkMock.Setup(u => u.QuotaUsages).Returns(_quotaUsageRepositoryMock.Object);

        // Transaction setups
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _subscriptionPlanQuotaRepositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid planId, CancellationToken _) => new SubscriptionPlan
            {
                Id = planId,
                IsActive = true
            });
        _subscriptionPlanQuotaRepositoryMock.Setup(repository => repository.GetActivePlanQuotaByCodeAsync(
                It.IsAny<Guid>(),
                IServiceCreditService.QuotaCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid planId, string _quotaCode, CancellationToken _cancellationToken) => new SubscriptionPlanQuota
            {
                PlanId = planId,
                QuotaId = _serviceCreditQuotaId,
                LimitValue = 10,
                IsActive = true,
                IsDeleted = false
            });
        _quotaUsageRepositoryMock.Setup(repository => repository.GetOrCreateAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscriptionUsage
            {
                Id = Guid.NewGuid(),
                QuotaId = _serviceCreditQuotaId,
                LimitValue = 10
            });

        // Default mock authenticated HttpContext
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = claimsPrincipal };
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        _service = new UserSubscriptionService(
            _unitOfWorkMock.Object,
            _payOsMock.Object,
            _paymentServiceMock.Object,
            _subscriptionPlanQuotaRepositoryMock.Object,
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
    public async Task CheckoutAsync_ClientTypeOmitted_UsesWebCallbacks()
    {
        var payOsRequest = await CheckoutAndCapturePayOsRequestAsync(
            new CheckoutSubscriptionRequest());

        Assert.Multiple(() =>
        {
            Assert.That(payOsRequest.UseMobileCallbacks, Is.False);
            Assert.That(payOsRequest.ReturnUrl, Is.Empty);
            Assert.That(payOsRequest.CancelUrl, Is.Empty);
        });
    }

    [Test]
    public async Task CheckoutAsync_ExplicitWebClient_UsesWebCallbacks()
    {
        var payOsRequest = await CheckoutAndCapturePayOsRequestAsync(
            new CheckoutSubscriptionRequest
            {
                ClientType = CheckoutClientType.Web
            });

        Assert.That(payOsRequest.UseMobileCallbacks, Is.False);
    }

    [Test]
    public async Task CheckoutAsync_MobileClient_UsesMobileCallbacks()
    {
        var payOsRequest = await CheckoutAndCapturePayOsRequestAsync(
            new CheckoutSubscriptionRequest
            {
                ClientType = CheckoutClientType.Mobile
            });

        Assert.That(payOsRequest.UseMobileCallbacks, Is.True);
    }

    [Test]
    public async Task CheckoutAsync_InvalidClientType_ReturnsErrorBeforeTransaction()
    {
        var result = await _service.CheckoutAsync(new CheckoutSubscriptionRequest
        {
            PlanId = Guid.NewGuid(),
            ClientType = (CheckoutClientType)999
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Contains.Item("ClientType is invalid."));
        });
        _subscriptionsMock.Verify(
            repository => repository.Add(It.IsAny<UserSubscription>()),
            Times.Never);
        _paymentsMock.Verify(
            repository => repository.Add(It.IsAny<Payment>()),
            Times.Never);
        _transactionsMock.Verify(
            repository => repository.Add(It.IsAny<PaymentTransaction>()),
            Times.Never);
        _payOsMock.Verify(
            service => service.CreatePaymentLinkAsync(
                It.IsAny<PayOSCreatePaymentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            unitOfWork => unitOfWork.BeginTransactionAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task CheckoutAsync_ExistingActivePackage_AllowsStackedPackageCheckout()
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

        _payOsMock.Setup(p => p.CreatePaymentLinkAsync(
                It.IsAny<PayOSCreatePaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSCreatePaymentResult
            {
                PaymentLinkId = "stacked-package-link",
                Status = "PENDING",
                CheckoutUrl = "https://pay.test/stacked-package"
            });

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        _subscriptionsMock.Verify(r => r.Add(It.Is<UserSubscription>(subscription =>
            subscription.UserId == _userId &&
            subscription.PlanId == planId &&
            subscription.Status == SubscriptionStatus.Pending)), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CheckoutAsync_LockedPlanMissingOrInactive_ReturnsErrorAndRollsBack(bool missing)
    {
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = planId,
            IsActive = true,
            IsDeleted = false,
            Price = 100000
        };
        _plansMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _subscriptionPlanQuotaRepositoryMock.Setup(repository => repository.GetPlanForUpdateAsync(
                planId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(missing ? null : new SubscriptionPlan { Id = planId, IsActive = false });

        var result = await _service.CheckoutAsync(new CheckoutSubscriptionRequest { PlanId = planId });

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Contains.Item("Subscription plan is not active."));
        });
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _subscriptionsMock.Verify(r => r.Add(It.IsAny<UserSubscription>()), Times.Never);
    }

    [Test]
    public async Task CheckoutAsync_ServiceCreditMappingMissing_ReturnsConfigurationErrorAndRollsBack()
    {
        var plan = SetupActivePaidPlan();
        _subscriptionPlanQuotaRepositoryMock.Setup(repository => repository.GetActivePlanQuotaByCodeAsync(
                plan.Id,
                IServiceCreditService.QuotaCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanQuota?)null);

        var result = await _service.CheckoutAsync(new CheckoutSubscriptionRequest { PlanId = plan.Id });

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Contains.Item("SERVICE_CREDIT_NOT_CONFIGURED"));
        });
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        _subscriptionsMock.Verify(r => r.Add(It.IsAny<UserSubscription>()), Times.Never);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task CheckoutAsync_ServiceCreditLimitNotPositive_ReturnsConfigurationErrorAndRollsBack(
        int limitValue)
    {
        var plan = SetupActivePaidPlan();
        _subscriptionPlanQuotaRepositoryMock.Setup(repository => repository.GetActivePlanQuotaByCodeAsync(
                plan.Id,
                IServiceCreditService.QuotaCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlanQuota
            {
                PlanId = plan.Id,
                QuotaId = _serviceCreditQuotaId,
                LimitValue = limitValue,
                IsActive = true
            });

        var result = await _service.CheckoutAsync(new CheckoutSubscriptionRequest { PlanId = plan.Id });

        Assert.That(result.Errors, Contains.Item("SERVICE_CREDIT_NOT_CONFIGURED"));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(CancellationToken.None), Times.Once);
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

        UserSubscription? createdSubscription = null;
        Payment? createdPayment = null;
        PaymentTransaction? createdTransaction = null;
        _subscriptionsMock.Setup(repository => repository.Add(It.IsAny<UserSubscription>()))
            .Callback<UserSubscription>(subscription => createdSubscription = subscription);
        _paymentsMock.Setup(repository => repository.Add(It.IsAny<Payment>()))
            .Callback<Payment>(payment => createdPayment = payment);
        _transactionsMock.Setup(repository => repository.Add(It.IsAny<PaymentTransaction>()))
            .Callback<PaymentTransaction>(transaction => createdTransaction = transaction);

        // Act
        var result = await _service.CheckoutAsync(req);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.PaymentUrl, Is.EqualTo("checkout_url_abc"));
        Assert.Multiple(() =>
        {
            Assert.That(createdSubscription, Is.Not.Null);
            Assert.That(createdSubscription!.Status, Is.EqualTo(SubscriptionStatus.Pending));
            Assert.That(createdSubscription.StartDate, Is.Null);
            Assert.That(createdSubscription.EndDate, Is.Null);
            Assert.That(createdSubscription.AutoRenew, Is.False);
            Assert.That(createdPayment, Is.Not.Null);
            Assert.That(createdPayment!.UserSubscriptionId, Is.EqualTo(createdSubscription.Id));
            Assert.That(createdPayment.Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(createdTransaction, Is.Not.Null);
            Assert.That(createdTransaction!.UserSubscriptionId, Is.EqualTo(createdSubscription.Id));
            Assert.That(createdTransaction.PaymentId, Is.EqualTo(createdPayment.Id));
        });

        _quotaUsageRepositoryMock.Verify(repository => repository.GetOrCreateAsync(
            createdSubscription!.Id,
            _serviceCreditQuotaId,
            It.IsAny<DateTime>(),
            null,
            10,
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task CancelAsync_PendingSubscription_CancelsAndReturnsUpdated()
    {
        // Arrange
        var subId = Guid.NewGuid();
        var sub = new UserSubscription { Id = subId, UserId = _userId, Status = SubscriptionStatus.Pending, AutoRenew = true };
        var updatedSub = new UserSubscription { Id = subId, UserId = _userId, Status = SubscriptionStatus.Cancelled, AutoRenew = false };

        _subscriptionsMock.SetupSequence(r => r.GetByIdWithPlanAsync(subId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sub)
            .ReturnsAsync(updatedSub);
        _paymentServiceMock
            .Setup(service => service.CancelPendingPayOSCheckoutAsync(
                subId,
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentReconciliationResult<PayOSPaymentStatusResponse>.Ok(
                    new PayOSPaymentStatusResponse
                    {
                        SubscriptionId = subId,
                        IsCancelled = true,
                        PaymentStatus = PaymentStatus.Cancelled.ToString(),
                        SubscriptionStatus = SubscriptionStatus.Cancelled.ToString()
                    }));

        // Act
        var result = await _service.CancelAsync(subId);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.Status, Is.EqualTo(SubscriptionStatus.Cancelled));
        Assert.That(result.Data.AutoRenew, Is.False);

        _paymentServiceMock.Verify(
            service => service.CancelPendingPayOSCheckoutAsync(
                subId,
                _userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task CancelAsync_ActiveSubscription_ReturnsPendingOnlyError()
    {
        var subId = Guid.NewGuid();
        _subscriptionsMock.Setup(repository => repository.GetByIdWithPlanAsync(
                subId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = subId,
                UserId = _userId,
                Status = SubscriptionStatus.Active
            });

        var result = await _service.CancelAsync(subId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Contains.Item("Only pending subscriptions can be cancelled."));
        });
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private SubscriptionPlan SetupActivePaidPlan()
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            IsDeleted = false,
            Price = 100000,
            PlanName = "Gold"
        };
        _plansMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        return plan;
    }

    private async Task<PayOSCreatePaymentRequest> CheckoutAndCapturePayOsRequestAsync(
        CheckoutSubscriptionRequest request)
    {
        var plan = SetupActivePaidPlan();
        request.PlanId = plan.Id;
        PayOSCreatePaymentRequest? capturedRequest = null;
        _transactionsMock.Setup(repository => repository.GetByTransactionReferenceAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        _payOsMock.Setup(service => service.CreatePaymentLinkAsync(
                It.IsAny<PayOSCreatePaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<PayOSCreatePaymentRequest, CancellationToken>(
                (payOsRequest, _) => capturedRequest = payOsRequest)
            .ReturnsAsync(new PayOSCreatePaymentResult
            {
                PaymentLinkId = "client-type-link",
                Status = "PENDING",
                CheckoutUrl = "https://pay.test/client-type"
            });

        var result = await _service.CheckoutAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(capturedRequest, Is.Not.Null);
        });
        return capturedRequest!;
    }
}

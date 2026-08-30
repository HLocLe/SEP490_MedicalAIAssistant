using MedMateAI.Application.Models.Sales;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;

namespace MedMateAI.Tests.Services;

[TestFixture]
public sealed class SaleRedemptionServiceTests
{
    private const decimal BasePrice = 20000m;
    private const int BaseCredit = 2;

    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<ISaleCampaignRepository> _campaignRepositoryMock = null!;
    private Mock<ISaleRedemptionRepository> _redemptionRepositoryMock = null!;
    private SaleRedemptionService _service = null!;
    private SubscriptionPlan _lockedPlan = null!;
    private Guid _userId;
    private DateTime _utcNow;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _campaignRepositoryMock = new Mock<ISaleCampaignRepository>();
        _redemptionRepositoryMock = new Mock<ISaleRedemptionRepository>();
        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.SaleCampaigns)
            .Returns(_campaignRepositoryMock.Object);
        _unitOfWorkMock.Setup(unitOfWork => unitOfWork.SaleRedemptions)
            .Returns(_redemptionRepositoryMock.Object);

        _redemptionRepositoryMock.Setup(repository => repository.LockUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _redemptionRepositoryMock.Setup(repository => repository.HasSuccessfulPurchaseAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _redemptionRepositoryMock.Setup(repository => repository.HasFirstPurchaseReservationAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _redemptionRepositoryMock.Setup(repository => repository.GetOccupancyAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SaleRedemptionOccupancy>());
        _campaignRepositoryMock.Setup(repository => repository.GetOfferCandidatesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SaleCampaignPlan>());

        _userId = Guid.NewGuid();
        _utcNow = new DateTime(2026, 8, 30, 1, 0, 0, DateTimeKind.Utc);
        _lockedPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            PlanName = "Standard",
            Price = BasePrice,
            IsActive = true,
            IsDeleted = false
        };
        _service = new SaleRedemptionService(_unitOfWorkMock.Object);
    }

    [Test]
    public async Task ReserveBestOfferAsync_StrictNoSaleStillCurrent_ReturnsNoOffer()
    {
        var result = await ReserveAsync(
            expectedOfferId: null,
            expectedEffectivePrice: BasePrice,
            expectedGrantedCredit: BaseCredit);

        Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.NoOffer));
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Never);
    }

    [Test]
    public async Task ReserveBestOfferAsync_StrictNoSaleWhenSaleAppears_ReturnsUnavailable()
    {
        SetupCurrentOffer(salePrice: 15000m, bonusCredit: 1);

        var result = await ReserveAsync(
            expectedOfferId: null,
            expectedEffectivePrice: BasePrice,
            expectedGrantedCredit: BaseCredit);

        Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.OfferUnavailable));
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Never);
    }

    [Test]
    public async Task ReserveBestOfferAsync_StrictNoSaleWhenPlanPriceChanged_ReturnsUnavailable()
    {
        _lockedPlan.Price = 18000m;

        var result = await ReserveAsync(
            expectedOfferId: null,
            expectedEffectivePrice: BasePrice,
            expectedGrantedCredit: BaseCredit);

        Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.OfferUnavailable));
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Never);
    }

    [Test]
    public async Task ReserveBestOfferAsync_StrictNoSaleWhenBaseCreditChanged_ReturnsUnavailable()
    {
        var result = await ReserveAsync(
            expectedOfferId: null,
            expectedEffectivePrice: BasePrice,
            expectedGrantedCredit: BaseCredit,
            currentBaseCredit: 4);

        Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.OfferUnavailable));
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Never);
    }

    [Test]
    public async Task ReserveBestOfferAsync_LegacySnapshotWithCurrentSale_ReservesOffer()
    {
        var offerId = SetupCurrentOffer(salePrice: 15000m, bonusCredit: 1);

        var result = await ReserveAsync(
            expectedOfferId: null,
            expectedEffectivePrice: null,
            expectedGrantedCredit: null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.Reserved));
            Assert.That(result.Offer!.OfferId, Is.EqualTo(offerId));
            Assert.That(result.Offer.FinalPrice, Is.EqualTo(15000m));
            Assert.That(result.Offer.GrantedCredit, Is.EqualTo(3));
        });
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Once);
    }

    [Test]
    public async Task ReserveBestOfferAsync_StrictSaleExactMatch_ReservesOffer()
    {
        var offerId = SetupCurrentOffer(salePrice: 15000m, bonusCredit: 1);

        var result = await ReserveAsync(
            offerId,
            expectedEffectivePrice: 15000m,
            expectedGrantedCredit: 3);

        Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.Reserved));
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Once);
    }

    [Test]
    public async Task ReserveBestOfferAsync_StrictSalePriceChanged_ReturnsUnavailable()
    {
        var offerId = SetupCurrentOffer(salePrice: 18000m, bonusCredit: 1);

        var result = await ReserveAsync(
            offerId,
            expectedEffectivePrice: 15000m,
            expectedGrantedCredit: 3);

        Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.OfferUnavailable));
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Never);
    }

    [Test]
    public async Task ReserveBestOfferAsync_StrictSaleCreditChanged_ReturnsUnavailable()
    {
        var offerId = SetupCurrentOffer(salePrice: 15000m, bonusCredit: 2);

        var result = await ReserveAsync(
            offerId,
            expectedEffectivePrice: 15000m,
            expectedGrantedCredit: 3);

        Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.OfferUnavailable));
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Never);
    }

    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, true, false)]
    public async Task ReserveBestOfferAsync_IncompleteSnapshot_ReturnsUnavailable(
        bool includeOfferId,
        bool includePrice,
        bool includeCredit)
    {
        var result = await ReserveAsync(
            includeOfferId ? Guid.NewGuid() : null,
            includePrice ? BasePrice : null,
            includeCredit ? BaseCredit : null);

        Assert.That(result.Outcome, Is.EqualTo(SaleReservationOutcome.OfferUnavailable));
        _redemptionRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<SaleRedemption>()),
            Times.Never);
    }

    private async Task<SaleReservationResult> ReserveAsync(
        Guid? expectedOfferId,
        decimal? expectedEffectivePrice,
        int? expectedGrantedCredit,
        int currentBaseCredit = BaseCredit)
    {
        return await _service.ReserveBestOfferAsync(
            _lockedPlan,
            currentBaseCredit,
            _userId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            expectedOfferId,
            expectedEffectivePrice,
            expectedGrantedCredit,
            _utcNow);
    }

    private Guid SetupCurrentOffer(decimal salePrice, int bonusCredit)
    {
        var campaignId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var campaign = new SaleCampaign
        {
            Id = campaignId,
            Name = "Current sale",
            EligibilityType = SaleCampaignEligibilityType.All,
            StartAt = _utcNow.AddHours(-1),
            EndAt = _utcNow.AddHours(1),
            IsActive = true,
            IsDeleted = false
        };
        var campaignPlan = new SaleCampaignPlan
        {
            Id = offerId,
            SaleCampaignId = campaignId,
            PlanId = _lockedPlan.Id,
            SalePrice = salePrice,
            BonusCredit = bonusCredit,
            IsActive = true,
            IsDeleted = false
        };

        _campaignRepositoryMock.Setup(repository => repository.GetOfferCandidatesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { campaignPlan });
        _campaignRepositoryMock.Setup(repository => repository.GetByIdForUpdateAsync(
                campaignId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);
        _campaignRepositoryMock.Setup(repository => repository.GetCampaignPlanAsync(
                campaignId,
                _lockedPlan.Id,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaignPlan);

        return offerId;
    }
}

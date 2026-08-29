using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Models.Sales;

public enum SaleReservationOutcome
{
    NoOffer = 0,
    Reserved = 1,
    OfferUnavailable = 2
}

public sealed record SaleReservationResult(
    SaleReservationOutcome Outcome,
    SaleOfferSnapshot? Offer,
    SaleRedemption? Redemption)
{
    public static SaleReservationResult NoOffer() =>
        new(SaleReservationOutcome.NoOffer, null, null);

    public static SaleReservationResult Unavailable() =>
        new(SaleReservationOutcome.OfferUnavailable, null, null);

    public static SaleReservationResult Reserved(
        SaleOfferSnapshot offer,
        SaleRedemption redemption) =>
        new(SaleReservationOutcome.Reserved, offer, redemption);
}

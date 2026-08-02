namespace MedMateAI.Application.DTOs.Payments.PayOS;

public sealed record PayOSPaymentLinkLookupResult(
    bool Success,
    PayOSPaymentLinkResult? Data,
    PayOSPaymentLinkLookupError Error = PayOSPaymentLinkLookupError.None)
{
    public static PayOSPaymentLinkLookupResult Ok(PayOSPaymentLinkResult data)
    {
        return new PayOSPaymentLinkLookupResult(true, data);
    }

    public static PayOSPaymentLinkLookupResult Fail(PayOSPaymentLinkLookupError error)
    {
        return new PayOSPaymentLinkLookupResult(false, null, error);
    }
}

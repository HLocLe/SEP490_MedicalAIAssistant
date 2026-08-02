namespace MedMateAI.Application.DTOs.Payments.PayOS;

public enum PayOSPaymentLinkLookupError
{
    None,
    NotFound,
    RateLimited,
    Unavailable,
    InvalidResponse
}

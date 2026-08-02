namespace MedMateAI.Application.Models.Payments;

public enum PaymentReconciliationErrorCode
{
    None,
    Unauthenticated,
    InvalidRequest,
    NotFound,
    Forbidden,
    ProviderNotFound,
    ProviderRateLimited,
    ProviderUnavailable,
    ProviderInvalidResponse,
    OrderCodeMismatch,
    AmountMismatch,
    Conflict
}

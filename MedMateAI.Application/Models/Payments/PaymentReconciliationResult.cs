namespace MedMateAI.Application.Models.Payments;

public sealed record PaymentReconciliationResult<T>(
    bool Success,
    T? Data,
    PaymentReconciliationErrorCode Error = PaymentReconciliationErrorCode.None,
    string? Message = null)
{
    public static PaymentReconciliationResult<T> Ok(T data)
    {
        return new PaymentReconciliationResult<T>(true, data);
    }

    public static PaymentReconciliationResult<T> Fail(
        PaymentReconciliationErrorCode error,
        string? message = null)
    {
        return new PaymentReconciliationResult<T>(false, default, error, message);
    }
}

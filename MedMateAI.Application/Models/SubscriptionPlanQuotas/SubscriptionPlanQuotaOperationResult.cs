namespace MedMateAI.Application.Models.SubscriptionPlanQuotas;

public sealed record SubscriptionPlanQuotaOperationResult<T>(
    bool Success,
    T? Data,
    SubscriptionPlanQuotaErrorCode Error = SubscriptionPlanQuotaErrorCode.None,
    string? Message = null)
{
    public static SubscriptionPlanQuotaOperationResult<T> Ok(T data)
    {
        return new SubscriptionPlanQuotaOperationResult<T>(true, data);
    }

    public static SubscriptionPlanQuotaOperationResult<T> Fail(
        SubscriptionPlanQuotaErrorCode error,
        string? message = null)
    {
        return new SubscriptionPlanQuotaOperationResult<T>(
            false,
            default,
            error,
            message);
    }
}

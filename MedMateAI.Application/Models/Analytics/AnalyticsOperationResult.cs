namespace MedMateAI.Application.Models.Analytics;

public sealed record AnalyticsOperationResult<T>(
    bool Success,
    T? Data,
    AnalyticsErrorCode Error = AnalyticsErrorCode.None,
    string? Message = null)
{
    public static AnalyticsOperationResult<T> Ok(T data, string? message = null) =>
        new(true, data, Message: message);

    public static AnalyticsOperationResult<T> Fail(
        AnalyticsErrorCode error,
        string? message = null) =>
        new(false, default, error, message);
}

namespace MedMateAI.Application.Models.ServiceCredits;

public sealed record ServiceCreditOperationResult<T>(
    bool Success,
    T? Data,
    ServiceCreditErrorCode Error = ServiceCreditErrorCode.None,
    bool IsReplay = false)
{
    public static ServiceCreditOperationResult<T> Ok(T data, bool isReplay = false) =>
        new(true, data, IsReplay: isReplay);

    public static ServiceCreditOperationResult<T> Fail(ServiceCreditErrorCode error) =>
        new(false, default, error);
}

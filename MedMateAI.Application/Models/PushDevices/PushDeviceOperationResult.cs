namespace MedMateAI.Application.Models.PushDevices;

public enum PushDeviceErrorCode
{
    None,
    Unauthenticated,
    InvalidRequest,
    NotFound,
    Conflict
}

public sealed record PushDeviceOperationResult<T>(
    bool Success,
    T? Data,
    PushDeviceErrorCode Error = PushDeviceErrorCode.None,
    string? Message = null)
{
    public static PushDeviceOperationResult<T> Ok(T data)
    {
        return new PushDeviceOperationResult<T>(true, data);
    }

    public static PushDeviceOperationResult<T> Fail(
        PushDeviceErrorCode error,
        string? message = null)
    {
        return new PushDeviceOperationResult<T>(false, default, error, message);
    }
}

namespace MedMateAI.Application.Models.UserMedications;

public enum UserMedicationErrorCode
{
    None,
    Unauthenticated,
    InvalidRequest,
    NotFound,
    Conflict
}

public sealed record UserMedicationOperationResult<T>(
    bool Success,
    T? Data,
    UserMedicationErrorCode Error = UserMedicationErrorCode.None,
    string? Message = null)
{
    public static UserMedicationOperationResult<T> Ok(T data)
    {
        return new UserMedicationOperationResult<T>(true, data);
    }

    public static UserMedicationOperationResult<T> Fail(
        UserMedicationErrorCode error,
        string? message = null)
    {
        return new UserMedicationOperationResult<T>(
            false,
            default,
            error,
            message);
    }
}

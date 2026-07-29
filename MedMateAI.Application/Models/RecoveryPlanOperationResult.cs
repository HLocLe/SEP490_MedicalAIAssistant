namespace MedMateAI.Application.Models;

public enum QuotaMutationStatus
{
    Applied,
    // A concurrent log conflict can occur after mutation; callers must roll back before replaying.
    Duplicate,
    Rejected
}

public enum RecoveryPlanErrorCode
{
    None, Unauthenticated, Forbidden, InvalidRequest, NotFound,
    NoActiveSubscription, RecoveryPlanQuotaNotConfigured, RecoveryPlanQuotaExhausted,
    IdempotencyKeyInvalid, InvalidRequestState, RecoveryPlanRequestAlreadyClaimed,
    DoctorProfileNotFound, DoctorNotActive, DoctorNotAcceptingRequests,
    DoctorCapacityReached, AssignmentExpired, QuotaMutationFailed, Conflict
}

public sealed record RecoveryPlanOperationResult<T>(
    bool Success, T? Data, RecoveryPlanErrorCode Error = RecoveryPlanErrorCode.None,
    bool IsReplay = false, string? Message = null)
{
    public static RecoveryPlanOperationResult<T> Ok(T data, bool replay = false) => new(true, data, IsReplay: replay);
    public static RecoveryPlanOperationResult<T> Fail(RecoveryPlanErrorCode error, string? message = null) => new(false, default, error, Message: message);
}

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class RecoveryPlanRequestReadinessResponse
{
    public bool IsReady { get; set; }

    public IReadOnlyList<RecoveryPlanRequestReadinessIssueResponse> Issues { get; set; } =
        Array.Empty<RecoveryPlanRequestReadinessIssueResponse>();
}

public sealed class RecoveryPlanRequestReadinessIssueResponse
{
    public string Code { get; set; } = string.Empty;

    public string Field { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

namespace MedMateAI.Application.Common;

public static class RecoveryPlanCancellationReasons
{
    public const int MaximumReasonCodeLength = 100;
    public const int MaximumReasonLength = 2000;

    public const string NoLongerNeeded = "NO_LONGER_NEEDED";
    public const string HealthConditionChanged = "HEALTH_CONDITION_CHANGED";
    public const string PlanNotSuitable = "PLAN_NOT_SUITABLE";
    public const string UnableToFollow = "UNABLE_TO_FOLLOW";
    public const string StartingOtherTreatment = "STARTING_OTHER_TREATMENT";
    public const string Other = "OTHER";

    public static bool TryNormalize(
        string? reasonCode,
        string? reason,
        out string normalizedReasonCode,
        out string? normalizedReason)
    {
        normalizedReasonCode = (reasonCode ?? string.Empty)
            .Trim()
            .ToUpperInvariant();
        normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim();

        if (normalizedReasonCode.Length is < 1 or > MaximumReasonCodeLength
            || normalizedReason?.Length > MaximumReasonLength
            || !IsSupported(normalizedReasonCode))
        {
            return false;
        }

        return normalizedReasonCode != Other || normalizedReason is not null;
    }

    public static string? GetDisplayLabel(string reasonCode) => reasonCode switch
    {
        NoLongerNeeded => "Không còn cần thiết",
        HealthConditionChanged => "Tình trạng sức khỏe đã thay đổi",
        PlanNotSuitable => "Kế hoạch không phù hợp",
        UnableToFollow => "Không thể tiếp tục thực hiện",
        StartingOtherTreatment => "Bắt đầu phương pháp điều trị khác",
        Other => "Lý do khác",
        _ => null
    };

    private static bool IsSupported(string reasonCode) => reasonCode is
        NoLongerNeeded
        or HealthConditionChanged
        or PlanNotSuitable
        or UnableToFollow
        or StartingOtherTreatment
        or Other;
}

namespace MedMateAI.Application.Models.Payments;

public sealed record PayOSPendingReconciliationSettings(
    int PaymentLinkExpirationMinutes,
    int MinimumAgeMinutes,
    int CleanupGraceMinutes,
    int BatchSize);

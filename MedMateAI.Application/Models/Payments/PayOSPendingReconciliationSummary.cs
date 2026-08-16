namespace MedMateAI.Application.Models.Payments;

public sealed record PayOSPendingReconciliationSummary(
    int CandidateCount,
    int PaidCount,
    int CancelledCount,
    int FailedCount,
    int StillPendingCount,
    int ProviderUnavailableCount,
    int RateLimitedCount,
    int InvalidCount);

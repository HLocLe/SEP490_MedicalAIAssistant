using MedMateAI.Application.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

internal static class RecoveryPlanJobRetrySchedule
{
    private const double JitterRatio = 0.1;
    private const int MaximumJitterSeconds = 5;

    public static DateTime GetRetryAtUtc(
        Guid itemId,
        int attemptCount,
        DateTime utcNow,
        RecoveryPlanJobOptions options)
    {
        var exponent = Math.Max(0, attemptCount - 1);
        var uncappedSeconds = options.RetryBaseSeconds * Math.Pow(2, exponent);
        var backoffSeconds = Math.Min(options.RetryMaxSeconds, uncappedSeconds);
        var jitterLimit = Math.Min(
            MaximumJitterSeconds,
            Math.Max(1, (int)Math.Ceiling(backoffSeconds * JitterRatio)));
        var jitterSeconds = GetDeterministicJitter(itemId, attemptCount, jitterLimit);
        var totalDelaySeconds = Math.Min(
            options.RetryMaxSeconds,
            backoffSeconds + jitterSeconds);

        return utcNow.AddSeconds(totalDelaySeconds);
    }

    private static int GetDeterministicJitter(
        Guid itemId,
        int attemptCount,
        int maximumSeconds)
    {
        var bytes = itemId.ToByteArray();
        var seed = BitConverter.ToUInt32(bytes, 0) + (uint)attemptCount;
        return (int)(seed % (uint)(maximumSeconds + 1));
    }
}

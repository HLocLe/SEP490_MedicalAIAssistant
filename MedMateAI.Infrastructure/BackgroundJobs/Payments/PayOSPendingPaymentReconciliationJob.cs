using Hangfire;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Payments;
using MedMateAI.Infrastructure.Payments.PayOS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs;

public sealed class PayOSPendingPaymentReconciliationJob
{
    private readonly IPaymentService _paymentService;
    private readonly PayOSOptions _options;
    private readonly ILogger<PayOSPendingPaymentReconciliationJob> _logger;

    public PayOSPendingPaymentReconciliationJob(
        IPaymentService paymentService,
        IOptions<PayOSOptions> options,
        ILogger<PayOSPendingPaymentReconciliationJob> logger)
    {
        _paymentService = paymentService;
        _options = options.Value;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var summary = await _paymentService.ReconcilePendingPayOSPaymentsAsync(
            new PayOSPendingReconciliationSettings(
                _options.PaymentLinkExpirationMinutes,
                _options.PendingReconciliationMinimumAgeMinutes,
                _options.PendingCleanupGraceMinutes,
                _options.PendingReconciliationBatchSize),
            cancellationToken);

        _logger.LogInformation(
            "Pending payOS maintenance completed: candidates {CandidateCount}, paid {PaidCount}, cancelled {CancelledCount}, failed {FailedCount}, still pending {StillPendingCount}, unavailable {ProviderUnavailableCount}, rate limited {RateLimitedCount}, invalid {InvalidCount}.",
            summary.CandidateCount,
            summary.PaidCount,
            summary.CancelledCount,
            summary.FailedCount,
            summary.StillPendingCount,
            summary.ProviderUnavailableCount,
            summary.RateLimitedCount,
            summary.InvalidCount);
    }
}

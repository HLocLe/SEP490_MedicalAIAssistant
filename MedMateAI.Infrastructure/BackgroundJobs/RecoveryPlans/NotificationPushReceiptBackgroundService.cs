using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using MedMateAI.Infrastructure.Push.Expo.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class NotificationPushReceiptBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoveryPlanJobOptions _jobOptions;
    private readonly ExpoPushOptions _pushOptions;
    private readonly ILogger<NotificationPushReceiptBackgroundService> _logger;

    public NotificationPushReceiptBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<RecoveryPlanJobOptions> jobOptions,
        IOptions<ExpoPushOptions> pushOptions,
        ILogger<NotificationPushReceiptBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _jobOptions = jobOptions.Value;
        _pushOptions = pushOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_jobOptions.NotificationPushReceiptProcessorEnabled
            || !_pushOptions.Enabled)
        {
            _logger.LogInformation("Push receipt processor is disabled.");
            await WaitForShutdownAsync(stoppingToken);
            return;
        }

        var pollingInterval =
            TimeSpan.FromSeconds(_jobOptions.NotificationPollingSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIterationAsync(stoppingToken);
            try
            {
                await Task.Delay(pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunIterationAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<INotificationPushReceiptProcessor>();
            await processor.ProcessBatchAsync(DateTime.UtcNow, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Push receipt iteration failed with {FailureType}.",
                exception.GetType().Name);
        }
    }

    private static async Task WaitForShutdownAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
    }
}

using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class RecoveryPlanLifecycleBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoveryPlanJobOptions _options;
    private readonly ILogger<RecoveryPlanLifecycleBackgroundService> _logger;

    public RecoveryPlanLifecycleBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<RecoveryPlanJobOptions> options,
        ILogger<RecoveryPlanLifecycleBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!HasEnabledProcessor())
        {
            _logger.LogInformation("Recovery Plan lifecycle workers are disabled.");
            await WaitForShutdownAsync(stoppingToken);
            return;
        }

        var pollingInterval = TimeSpan.FromSeconds(_options.LifecyclePollingSeconds);
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
        if (_options.AssignmentTimeoutWorkerEnabled)
        {
            await RunAssignmentTimeoutAsync(stoppingToken);
        }

        if (_options.PlanCompletionWorkerEnabled)
        {
            await RunPlanCompletionAsync(stoppingToken);
        }
    }

    private async Task RunAssignmentTimeoutAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<IRecoveryPlanAssignmentTimeoutProcessor>();
            await processor.ProcessBatchAsync(DateTime.UtcNow, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Recovery Plan assignment timeout iteration failed with {FailureType}.",
                exception.GetType().Name);
        }
    }

    private async Task RunPlanCompletionAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<IRecoveryPlanCompletionProcessor>();
            await processor.ProcessBatchAsync(DateTime.UtcNow, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Recovery Plan completion iteration failed with {FailureType}.",
                exception.GetType().Name);
        }
    }

    private bool HasEnabledProcessor()
    {
        return _options.AssignmentTimeoutWorkerEnabled
            || _options.PlanCompletionWorkerEnabled;
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

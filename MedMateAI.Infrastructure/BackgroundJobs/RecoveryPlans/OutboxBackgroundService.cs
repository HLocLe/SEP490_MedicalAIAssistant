using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoveryPlanJobOptions _options;
    private readonly ILogger<OutboxBackgroundService> _logger;

    public OutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<RecoveryPlanJobOptions> options,
        ILogger<OutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.OutboxProcessorEnabled)
        {
            _logger.LogInformation("Recovery Plan outbox processor is disabled.");
            await WaitForShutdownAsync(stoppingToken);
            return;
        }

        var pollingInterval = TimeSpan.FromSeconds(_options.OutboxPollingSeconds);
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
                .GetRequiredService<IOutboxMessageProcessor>();
            await processor.ProcessBatchAsync(DateTime.UtcNow, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Recovery Plan outbox iteration failed with {FailureType}.",
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

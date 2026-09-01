using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.Sales;

public sealed class SaleCampaignNotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SaleCampaignNotificationOptions _options;
    private readonly ILogger<SaleCampaignNotificationBackgroundService> _logger;

    public SaleCampaignNotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<SaleCampaignNotificationOptions> options,
        ILogger<SaleCampaignNotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Sale campaign notification scheduler is disabled.");
            await WaitForShutdownAsync(stoppingToken);
            return;
        }

        var pollingInterval = TimeSpan.FromSeconds(_options.PollingSeconds);
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
            var scheduler = scope.ServiceProvider
                .GetRequiredService<ISaleCampaignNotificationScheduler>();
            await scheduler.ScheduleAsync(DateTime.UtcNow, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Sale campaign notification scheduler iteration failed with {FailureType}.",
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

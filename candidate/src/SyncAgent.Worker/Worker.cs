using Microsoft.Extensions.Options;
using SyncAgent.Application.Services;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Worker;

public sealed class SyncAgentWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PollingOptions> pollingOptions,
    ILogger<SyncAgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(pollingOptions.Value.IntervalSeconds);
        logger.LogInformation("SyncAgent worker started, polling every {IntervalSeconds}s", interval.TotalSeconds);

        // PeriodicTimer waits between cycles without blocking a thread, and a cycle that runs long
        // simply delays the next tick rather than overlapping with it.
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunPollCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host asked the worker to stop.
        }
        finally
        {
            logger.LogInformation("SyncAgent worker stopped");
        }
    }

    private async Task RunPollCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The poller and its dependencies are Scoped, so each cycle gets its own scope:
            // a hosted service is a Singleton and cannot consume them directly.
            using var scope = scopeFactory.CreateScope();
            var poller = scope.ServiceProvider.GetRequiredService<ISyncTaskPoller>();
            await poller.PollAndExecuteAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One bad cycle must not stop the agent; the next tick tries again.
            logger.LogError(ex, "Unexpected error during the poll cycle");
        }
    }
}

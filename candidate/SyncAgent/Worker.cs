using Microsoft.Extensions.Options;
using SyncAgent.Api;
using SyncAgent.Configuration;
using SyncAgent.Handlers;

namespace SyncAgent;

/// <summary>
/// Long-running polling loop: fetches the next pending sync task from the SyncPlatform
/// server, dispatches it to the matching handler, and posts the result back.
/// A fresh DI scope is created per cycle so that scoped services (DbContext, repository,
/// handlers) never leak across iterations. Failures while talking to the SyncPlatform
/// server or processing a task are logged and swallowed so the worker keeps polling.
/// </summary>
public class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncAgentOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextTaskAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while polling for the next sync task.");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessNextTaskAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<ISyncPlatformApiClient>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<TaskHandlerDispatcher>();

        var task = await apiClient.GetNextTaskAsync(stoppingToken);
        if (task is null)
        {
            logger.LogDebug("No pending sync task available.");
            return;
        }

        logger.LogInformation("Processing task {TaskId} of type {TaskType}.", task.TaskId, task.TaskType);

        var result = await dispatcher.DispatchAsync(task, stoppingToken);

        await apiClient.PostResultAsync(result, stoppingToken);

        logger.LogInformation(
            "Task {TaskId} completed with status {Status} ({RecordCount} records).",
            result.TaskId, result.Status, result.RecordCount);
    }
}

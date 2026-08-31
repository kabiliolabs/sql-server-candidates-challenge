using System.Collections;
using Microsoft.Extensions.Options;
using SyncAgent.Worker.Configuration;
using SyncAgent.Worker.Platform;
using SyncAgent.Worker.Tasks;

namespace SyncAgent.Worker;

/// <summary>
/// Polls the SyncPlatform for pending tasks, executes the matching query via
/// ISyncTaskDispatcher, and posts the outcome back - "completed" with the data on
/// success, "failed" with an error message if the query itself throws.
/// </summary>
public class Worker(
    ISyncPlatformClient platformClient,
    ISyncTaskDispatcher taskDispatcher,
    IOptions<SyncPlatformOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly SyncPlatformOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sync Agent starting up.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await platformClient.GetNextTaskAsync(stoppingToken);

                if (task is null)
                {
                    logger.LogInformation("No pending tasks.");
                }
                else
                {
                    logger.LogInformation(
                        "Received task {TaskId} ({TaskType}), created at {CreatedAt}.",
                        task.TaskId, task.TaskType, task.CreatedAt);

                    await ProcessTaskAsync(task, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failure polling or posting to the platform itself (network blip,
                // platform down, bad response) should not crash the always-on agent -
                // log it and try again next interval. Failures executing a specific
                // task's query are handled inside ProcessTaskAsync instead, since those
                // get reported back to the platform as a "failed" result, not just logged.
                logger.LogError(ex, "Polling the SyncPlatform failed.");
            }

            try
            {
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the host is shutting down mid-delay.
            }
        }

        logger.LogInformation("Sync Agent shutting down.");
    }

    internal async Task ProcessTaskAsync(SyncTask task, CancellationToken cancellationToken)
    {
        SyncResult result;
        try
        {
            var data = await taskDispatcher.DispatchAsync(task, cancellationToken);
            var recordCount = (data as ICollection)?.Count ?? 0;
            result = SyncResult.Completed(task, data, recordCount);

            logger.LogInformation("Executed task {TaskId}: {RecordCount} record(s) retrieved.", task.TaskId, recordCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = SyncResult.Failed(task, ex.Message);
            logger.LogError(ex, "Failed to execute task {TaskId} ({TaskType}).", task.TaskId, task.TaskType);
        }

        await platformClient.PostResultAsync(result, cancellationToken);
        logger.LogInformation("Posted {Status} result for task {TaskId}.", result.Status, task.TaskId);
    }
}

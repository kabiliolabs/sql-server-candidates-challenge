using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;
using SyncAgent.Infrastructure.Http;
using SyncAgent.Services;

namespace SyncAgent.Worker;

public sealed class PollingBackgroundService(ISyncPlatformClient syncPlatformClient, ISyncTaskExecutor syncTaskExecutor, 
    IOptions<SyncPlatformOptions> options, ILogger<PollingBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sync agent started. Poll interval: {PollInterval}.", _pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await syncPlatformClient.GetNextTaskAsync(stoppingToken);

                if (task is null)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                    continue;
                }

                logger.LogInformation("Received task {TaskId} of type {TaskType}.", task.TaskId, task.TaskType);

                var result = await syncTaskExecutor.ExecuteAsync(task, stoppingToken);
                await syncPlatformClient.SubmitResultAsync(result, stoppingToken);

                logger.LogInformation(
                    "Task {TaskId} finished with status {Status} and {RecordCount} records.",
                    result.TaskId,
                    result.Status,
                    result.RecordCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Sync agent is stopping.");
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected error during polling loop.");
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }
    }
}

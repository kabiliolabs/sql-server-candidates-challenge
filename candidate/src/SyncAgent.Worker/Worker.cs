using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Dispatch;

namespace SyncAgent.Worker;

/// <summary>Polls the platform, dispatches whatever task comes back, and always posts a
/// result — 204 just means "keep polling," and any failure (unknown task type, SQL error,
/// timeout) becomes a "failed" result rather than an unhandled exception, since the platform
/// simulator has no requeue: a task that's dequeued and never posted back is lost for good.</summary>
public sealed class Worker(
    ISyncPlatformClient platformClient,
    SyncTaskDispatcher dispatcher,
    IOptions<SyncAgentOptions> options,
    ILogger<Worker> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    // Defaults to the real clock in production (DI never supplies this parameter); tests pass
    // a FakeTimeProvider so the poll loop can be driven deterministically by advancing virtual
    // time instead of waiting on real wall-clock delays.
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
        logger.LogInformation("Sync agent started. Polling every {PollIntervalSeconds}s.", options.Value.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Getting the next task or posting the result failed outright (e.g. platform
                // unreachable) — nothing to post back to in that case. Log and keep polling;
                // don't let a transient platform outage kill the whole worker.
                logger.LogError(ex, "Unexpected error in polling loop.");
            }

            try
            {
                await Task.Delay(pollInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var task = await platformClient.GetNextTaskAsync(cancellationToken);
        if (task is null)
        {
            logger.LogDebug("No task queued.");
            return;
        }

        logger.LogInformation("Received task {TaskType} ({TaskId}).", task.TaskType, task.TaskId);

        var result = await dispatcher.DispatchAsync(task, cancellationToken);

        if (result.Status == Core.Tasks.SyncResultStatus.Failed)
        {
            logger.LogWarning("Task {TaskType} ({TaskId}) failed: {ErrorMessage}", task.TaskType, task.TaskId, result.ErrorMessage);
        }
        else
        {
            logger.LogInformation("Task {TaskType} ({TaskId}) completed with {RecordCount} records.", task.TaskType, task.TaskId, result.RecordCount);
        }

        await platformClient.PostResultAsync(result, cancellationToken);
    }
}

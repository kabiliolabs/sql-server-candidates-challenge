using Microsoft.Extensions.Options;

using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Models;
using SyncAgent.Core.Validation;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Worker
{
    public sealed class PollingWorker(
        IPlatformClient platformClient,
        ISyncTaskDispatcher dispatcher,
        IOptions<PlatformOptions> options,
        ILogger<PollingWorker> logger) : BackgroundService
    {
        private const int MaxRememberedTaskIds = 1000;
        private readonly Queue<string> _recentTaskOrder = new();
        private readonly HashSet<string> _recentTaskIds = new(StringComparer.Ordinal);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);
            var maxBackoff = TimeSpan.FromSeconds(options.Value.MaxBackoffSeconds);
            var retryDelay = interval;

            logger.LogInformation("Sync agent started. Polling {BaseUrl} every {Seconds}s",
                options.Value.BaseUrl, interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var task = await platformClient.GetNextTaskAsync(stoppingToken);
                    retryDelay = interval;

                    if (task is not null)
                    {
                        await ProcessAsync(task, stoppingToken);
                        continue;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Sync cycle failed. Retrying in {Seconds}s", retryDelay.TotalSeconds);
                    await DelaySafeAsync(retryDelay, stoppingToken);
                    retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, maxBackoff.Ticks));
                    continue;
                }

                await DelaySafeAsync(interval, stoppingToken);
            }

            logger.LogInformation("Sync agent stopped.");
        }

        private async Task ProcessAsync(SyncTask task, CancellationToken cancellationToken)
        {
            if (!TryRemember(task.TaskId))
            {
                logger.LogWarning("Task {TaskId} was already processed. Skipping.", LogSanitizer.Sanitize(task.TaskId));
                return;
            }

            logger.LogInformation(
                "Received task {TaskId} ({TaskType})", LogSanitizer.Sanitize(task.TaskId), LogSanitizer.Sanitize(task.TaskType));

            var result = await dispatcher.DispatchAsync(task, cancellationToken);
            await platformClient.PostResultAsync(result, cancellationToken);

            logger.LogInformation("Posted result for task {TaskId}: {Status}, {Count} records",
                LogSanitizer.Sanitize(task.TaskId), result.Status, result.RecordCount);
        }

        private bool TryRemember(string? taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return true;

            if (!_recentTaskIds.Add(taskId))
                return false;

            _recentTaskOrder.Enqueue(taskId);
            if (_recentTaskOrder.Count > MaxRememberedTaskIds)
                _recentTaskIds.Remove(_recentTaskOrder.Dequeue());

            return true;
        }

        private static async Task DelaySafeAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            try { await Task.Delay(delay, cancellationToken); }
            catch (OperationCanceledException) { /* shutting down */ }
        }
    }
}
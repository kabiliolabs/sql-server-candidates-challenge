using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SyncAgent.Domain;
using SyncAgent.Protocol;

namespace SyncAgent
{
    /// <summary>Always-on poll loop. Each iteration is fully isolated: any failure is
    /// logged and the loop continues. Dedupe by taskId (forward-looking safety).</summary>
    public sealed class SyncAgentWorker
    {
        private readonly SyncPlatformClient _platform;
        private readonly SyncTaskDispatcher _dispatcher;
        private readonly AgentOptions _options;
        private readonly ILogger<SyncAgentWorker> _logger;
        private readonly HashSet<string> _seen = new HashSet<string>();

        public SyncAgentWorker(
            SyncPlatformClient platform,
            SyncTaskDispatcher dispatcher,
            AgentOptions options,
            ILogger<SyncAgentWorker> logger)
        {
            _platform = platform;
            _dispatcher = dispatcher;
            _options = options;
            _logger = logger;
        }

        public async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    SyncTask task = await _platform.GetNextTaskAsync(cancellationToken).ConfigureAwait(false);
                    if (task == null)
                    {
                        await Task.Delay(_options.PollIntervalMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (!_seen.Add(task.taskId))
                    {
                        _logger.LogWarning("Duplicate task {TaskId} skipped.", task.taskId);
                        continue;
                    }

                    SyncResult result = await _dispatcher.DispatchAsync(task, cancellationToken).ConfigureAwait(false);
                    bool accepted = await _platform.PostResultAsync(result, cancellationToken).ConfigureAwait(false);
                    if (!accepted)
                    {
                        _logger.LogWarning("Result for task {TaskId} ({TaskType}) was not accepted.",
                            task.taskId, task.taskType);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break; // clean shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sync loop iteration failed; continuing.");
                    try
                    {
                        await Task.Delay(_options.PollIntervalMs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            _logger.LogInformation("Sync loop stopped.");
        }
    }
}
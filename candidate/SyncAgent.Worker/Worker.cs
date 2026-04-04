using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Worker;

public class Worker : BackgroundService
{
    private readonly IPlatformClient _platformClient;
    private readonly IReadOnlyDictionary<string, ISyncTaskHandler> _handlers;
    private readonly SyncAgentOptions _options;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IPlatformClient platformClient,
        IEnumerable<ISyncTaskHandler> handlers,
        IOptions<SyncAgentOptions> options,
        ILogger<Worker> logger)
    {
        _platformClient = platformClient;
        // Build a lookup dictionary — unknown task types are rejected before execution (whitelist)
        _handlers = handlers.ToDictionary(h => h.TaskType, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync agent started. Polling every {Interval}s", _options.PollingIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollingIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollOnceAsync(stoppingToken);
        }
    }

    internal async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        SyncTask? task = null;
        try
        {
            task = await _platformClient.GetNextTaskAsync(cancellationToken);

            if (task is null)
            {
                _logger.LogDebug("No tasks in queue");
                return;
            }

            _logger.LogInformation("Received task {TaskId} [{TaskType}]", task.TaskId, task.TaskType);

            // Whitelist check — reject unknown task types before executing anything
            if (!_handlers.TryGetValue(task.TaskType, out var handler))
            {
                _logger.LogWarning("Unknown task type received: {TaskType}", task.TaskType);
                await _platformClient.PostResultAsync(
                    SyncResult.Failure(task, $"Unsupported task type: {task.TaskType}"),
                    cancellationToken);
                return;
            }

            var data = await handler.HandleAsync(task.Parameters, cancellationToken);
            var result = SyncResult.Success(task, data);

            await _platformClient.PostResultAsync(result, cancellationToken);

            _logger.LogInformation("Task {TaskId} completed — {Count} records sent", task.TaskId, result.RecordCount);
        }
        catch (Exception ex) when (task is not null)
        {
            // Task was received but execution failed — report failure to the platform
            _logger.LogError(ex, "Task {TaskId} failed", task.TaskId);
            try
            {
                await _platformClient.PostResultAsync(
                    SyncResult.Failure(task, ex.Message),
                    cancellationToken);
            }
            catch (Exception postEx)
            {
                _logger.LogError(postEx, "Failed to post error result for task {TaskId}", task.TaskId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Polling itself failed — log and continue, don't crash the agent
            _logger.LogError(ex, "Error polling for tasks");
        }
    }
}

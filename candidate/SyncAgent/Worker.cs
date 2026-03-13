using Microsoft.Extensions.Options;
using SyncAgent.Configuration;
using SyncAgent.Models;
using SyncAgent.Services;

namespace SyncAgent;

/// <summary>
/// Always-on background worker that polls the platform for sync tasks,
/// executes them against the local SQL Server, and posts results back.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IPlatformApiClient _platformClient;
    private readonly ITaskHandlerFactory _handlerFactory;
    private readonly ILogger<Worker> _logger;
    private readonly TimeSpan _pollInterval;

    public Worker(
        IPlatformApiClient platformClient,
        ITaskHandlerFactory handlerFactory,
        IOptions<SyncAgentOptions> options,
        ILogger<Worker> logger)
    {
        _platformClient = platformClient;
        _handlerFactory = handlerFactory;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync Agent started. Polling every {Interval}s.", _pollInterval.TotalSeconds);

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
                _logger.LogError(ex, "Unexpected error in polling loop.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("Sync Agent stopped.");
    }

    private async Task ProcessNextTaskAsync(CancellationToken stoppingToken)
    {
        SyncTask? task;
        try
        {
            task = await _platformClient.GetNextTaskAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to poll platform for next task.");
            return;
        }

        if (task is null)
        {
            _logger.LogDebug("No tasks queued.");
            return;
        }

        _logger.LogInformation("Received task {TaskId} of type {TaskType}.", task.TaskId, task.TaskType);

        SyncResult result;
        try
        {
            if (!_handlerFactory.HasHandler(task.TaskType))
            {
                _logger.LogWarning("Unknown task type '{TaskType}'. Reporting as failed.", task.TaskType);
                result = SyncResult.Failure(task.TaskId, task.TaskType,
                    $"Task type '{task.TaskType}' is not supported by this agent.");
            }
            else
            {
                var handler = _handlerFactory.GetHandler(task.TaskType);
                result = await handler.ExecuteAsync(task, stoppingToken);
                _logger.LogInformation("Task {TaskId} executed successfully. Records: {Count}.",
                    task.TaskId, result.RecordCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task {TaskId} ({TaskType}).", task.TaskId, task.TaskType);
            result = SyncResult.Failure(task.TaskId, task.TaskType, ex.Message);
        }

        try
        {
            await _platformClient.PostResultAsync(result, stoppingToken);
            _logger.LogInformation("Result for task {TaskId} posted. Status: {Status}.", task.TaskId, result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post result for task {TaskId}.", task.TaskId);
        }
    }
}

using Microsoft.Extensions.Options;
using SyncAgent.Api;
using SyncAgent.Configuration;
using SyncAgent.Models;
using SyncAgent.Services;

namespace SyncAgent;

public sealed class Worker : BackgroundService
{
    private readonly ISyncPlatformClient _platformClient;
    private readonly ISyncTaskDispatcher _dispatcher;
    private readonly SyncPlatformOptions _options;
    private readonly ILogger<Worker> _logger;

    public Worker(
        ISyncPlatformClient platformClient,
        ISyncTaskDispatcher dispatcher,
        IOptions<SyncPlatformOptions> options,
        ILogger<Worker> logger)
    {
        _platformClient = platformClient;
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Sync Agent started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await _platformClient.GetNextTaskAsync(
                    stoppingToken);

                if (task is not null)
                {
                    await ProcessTaskAsync(
                        task,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error communicating with SyncPlatform.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(
                    _options.PollingIntervalSeconds),
                stoppingToken);
        }
    }

    private async Task ProcessTaskAsync(
        SyncTask task,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing task {TaskId} ({TaskType})",
            task.TaskId,
            task.TaskType);

        SyncResult result;

        try
        {
            ValidateTask(task);

            var execution = await _dispatcher.ExecuteAsync(
                task,
                cancellationToken);

            result = new SyncResult
            {
                TaskId = task.TaskId,
                TaskType = task.TaskType,
                Status = "completed",
                Data = execution.Data,
                RecordCount = execution.RecordCount,
                ExecutedAt = DateTimeOffset.UtcNow,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Task {TaskId} failed.",
                task.TaskId);

            result = new SyncResult
            {
                TaskId = task.TaskId,
                TaskType = task.TaskType,
                Status = "failed",
                Data = null,
                RecordCount = 0,
                ExecutedAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }

        await _platformClient.SubmitResultAsync(
            result,
            cancellationToken);
    }

    private static void ValidateTask(SyncTask task)
    {
        if (string.IsNullOrWhiteSpace(task.TaskId))
        {
            throw new ArgumentException(
                "TaskId is required.");
        }

        if (string.IsNullOrWhiteSpace(task.TaskType))
        {
            throw new ArgumentException(
                "TaskType is required.");
        }

        if (task.Parameters.ModifiedSince is null)
        {
            throw new ArgumentException(
                "modifiedSince is required.");
        }
    }
}
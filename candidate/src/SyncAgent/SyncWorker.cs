using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SyncAgent.Api;
using SyncAgent.Configuration;
using SyncAgent.Models;
using SyncAgent.Services;

namespace SyncAgent;

public sealed class SyncWorker : BackgroundService
{
    private readonly ISyncPlatformClient _platformClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncPlatformOptions _options;
    private readonly ILogger<SyncWorker> _logger;

    public SyncWorker(ISyncPlatformClient platformClient, IServiceScopeFactory scopeFactory, IOptions<SyncPlatformOptions> options, ILogger<SyncWorker> logger)
    {
        _platformClient = platformClient;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SyncWorker started");

        while (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug($"Running SyncWorker process");

            try
            {
                var task = await _platformClient.GetNextTaskAsync(cancellationToken);

                if (task is not null)
                    await ProcessTaskAsync(task, cancellationToken);
            }
            catch (OperationCanceledException)                
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Detected cancellation of the process");
                    break;
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error communicating with SyncPlatform.");
            }

            _logger.LogDebug($"Next iteration will be at: {DateTime.Now.AddMilliseconds(_options.PollingIntervalMSec).ToString("HH:mm:ss.fff")}");
            await Task.Delay(_options.PollingIntervalMSec, cancellationToken);
        }
    }

    private async Task ProcessTaskAsync(SyncTask task, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        ISyncTaskDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ISyncTaskDispatcher>();

        _logger.LogInformation($"Processing task {task.TaskId} ({task.TaskType})");

        SyncResult result;

        try
        {
            ValidateTask(task);

            var execution = await dispatcher.ExecuteAsync(task, cancellationToken);

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
            _logger.LogError(ex, $"Task {task.TaskId} failed.");

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

        try
        {
            await _platformClient.SubmitResultAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to submit result for task {result.TaskId} ({result.TaskType}).");
            return;
        }

        _logger.LogInformation($"Task {result.TaskId} ({result.TaskType}) finished with status {result.Status}. Records: {result.RecordCount}");
    }

    private static void ValidateTask(SyncTask task)
    {
        if (string.IsNullOrWhiteSpace(task.TaskId))
            throw new ArgumentException("TaskId is required.");

        if (string.IsNullOrWhiteSpace(task.TaskType))
            throw new ArgumentException("TaskType is required.");

        if (!task.Parameters.ModifiedSince.HasValue)
            throw new ArgumentException("modifiedSince is required.");
    }
}
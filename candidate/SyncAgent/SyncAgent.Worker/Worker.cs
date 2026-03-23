using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure;
using SyncAgent.Worker;

namespace SyncAgent.Worker;

public class SyncWorker : BackgroundService
{
    private readonly IPlatformClient _platform;
    private readonly SyncDispatcher _dispatcher;
    private readonly ISyncTaskValidator _taskValidator;
    private readonly ILogger<SyncWorker> _logger;
    private readonly TimeSpan _pollInterval;

    public SyncWorker(
        IPlatformClient platform,
        SyncDispatcher dispatcher,
        ISyncTaskValidator taskValidator,
        IOptions<PlatformOptions> options,
        ILogger<SyncWorker> logger)
    {
        _platform = platform;
        _dispatcher = dispatcher;
        _taskValidator = taskValidator;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncAgent started. Polling every {Interval}s", _pollInterval.TotalSeconds);

        try
        {
            await PollOnceAsync(stoppingToken);

            using var timer = new PeriodicTimer(_pollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PollOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation("SyncAgent stopped.");
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            var task = await _platform.GetNextTaskAsync(stoppingToken);

            if (task is null)
            {
                _logger.LogDebug("No task available.");
                return;
            }

            var validation = _taskValidator.Validate(task);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Rejected invalid task {TaskId}: {Error}", task.TaskId, validation.ErrorMessage);
                await _platform.PostResultAsync(new SyncResult
                {
                    TaskId = task.TaskId,
                    TaskType = task.TaskType,
                    Status = "failed",
                    ErrorMessage = validation.ErrorMessage
                }, stoppingToken);
                return;
            }

            var result = await _dispatcher.DispatchAsync(task, stoppingToken);
            await _platform.PostResultAsync(result, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in polling loop iteration.");
        }
    }
}

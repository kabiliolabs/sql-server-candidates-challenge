using Microsoft.Extensions.Options;
using TaskExecutionService.Configuration;
using TaskExecutionService.Platform;
using TaskExecutionService.Sync;

namespace TaskExecutionService;

/// <summary>
/// Always-on polling loop: asks the platform for the next task, dispatches it to
/// the right executor, and posts the result back. A failure in one cycle never
/// stops the agent — it logs, backs off, and keeps polling.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly IPlatformClient _platform;
    private readonly ITaskDispatcher _dispatcher;
    private readonly PlatformOptions _options;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IPlatformClient platform,
        ITaskDispatcher dispatcher,
        IOptions<PlatformOptions> options,
        ILogger<Worker> logger)
    {
        _platform = platform;
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
        _logger.LogInformation(
            "Sync agent started. Polling {BaseUrl} every {Seconds}s.", _options.BaseUrl, idleDelay.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await _platform.GetNextTaskAsync(stoppingToken);
                if (task is null)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(task.TaskId))
                {
                    // Without an id the platform cannot match a result, so a POST would just be
                    // rejected — skip it rather than do pointless work.
                    _logger.LogWarning("Received a task without a taskId; skipping it.");
                    continue;
                }

                _logger.LogInformation("Picked up task {TaskType} ({TaskId}).", task.TaskType, task.TaskId);

                var result = await _dispatcher.DispatchAsync(task, stoppingToken);
                await _platform.PostResultAsync(result, stoppingToken);

                // Drain quickly: loop straight back in case more tasks are queued.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling cycle failed; backing off {Seconds}s before retry.", idleDelay.TotalSeconds);
                await SafeDelayAsync(idleDelay, stoppingToken);
            }
        }

        _logger.LogInformation("Sync agent stopping.");
    }

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down — nothing to do.
        }
    }
}

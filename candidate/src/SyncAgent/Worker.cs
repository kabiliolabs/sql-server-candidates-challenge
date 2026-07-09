using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;
using SyncAgent.Platform;
using SyncAgent.Sync;

namespace SyncAgent;

/// <summary>
/// The always-on loop: ask the platform for work, execute it, post the result, repeat.
/// </summary>
/// <remarks>
/// <para>
/// The loop's contract is that it does not stop. A database outage, a platform outage or a single
/// poisonous task are all expected conditions on a client machine that nobody is watching. Each is
/// logged and the loop backs off; only host shutdown ends it.
/// </para>
/// <para>
/// After a task is processed the loop polls again immediately, which drains a backlog at full
/// speed. It sleeps for <see cref="AgentOptions.PollInterval"/> only when the queue is empty, so
/// an idle agent costs one request per interval rather than spinning.
/// </para>
/// </remarks>
internal sealed class Worker : BackgroundService
{
    private readonly IPlatformClient _platformClient;
    private readonly ISyncTaskDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<Worker> _logger;
    private readonly TimeSpan _pollInterval;

    public Worker(
        IPlatformClient platformClient,
        ISyncTaskDispatcher dispatcher,
        IOptions<AgentOptions> agentOptions,
        TimeProvider timeProvider,
        ILogger<Worker> logger)
    {
        ArgumentNullException.ThrowIfNull(agentOptions);

        _platformClient = platformClient ?? throw new ArgumentNullException(nameof(platformClient));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollInterval = agentOptions.Value.PollInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync agent started. Polling every {PollInterval}.", _pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool taskProcessed;

            try
            {
                taskProcessed = await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Typically the platform being unreachable, or a result the platform rejected.
                // Back off for one interval rather than hammering an endpoint that is already unwell.
                _logger.LogError(ex, "Poll cycle failed. Retrying after {PollInterval}.", _pollInterval);
                taskProcessed = false;
            }

            if (taskProcessed)
            {
                // More work may be queued behind this one; go straight back for it.
                continue;
            }

            if (!await DelayAsync(stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }

        _logger.LogInformation("Sync agent stopped.");
    }

    /// <returns><see langword="true"/> if a task was dequeued and handled; <see langword="false"/> if the queue was empty.</returns>
    private async Task<bool> PollOnceAsync(CancellationToken cancellationToken)
    {
        var task = await _platformClient.GetNextTaskAsync(cancellationToken).ConfigureAwait(false);

        if (task is null)
        {
            return false;
        }

        // The dispatcher turns a handler failure into a "failed" result, so a null here means only
        // one thing: the task carried no id to report against. It has already been logged.
        var result = await _dispatcher.DispatchAsync(task, cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            return true;
        }

        await _platformClient.PostResultAsync(result, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <returns><see langword="false"/> if the wait was cut short by shutdown.</returns>
    private async Task<bool> DelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            // TimeProvider rather than a bare Task.Delay so tests can advance a fake clock
            // instead of sleeping in real time.
            await Task.Delay(_pollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

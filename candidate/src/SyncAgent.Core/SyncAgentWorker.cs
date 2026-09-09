using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Exceptions;
using SyncAgent.Core.Models;

namespace SyncAgent.Core
{
    /// <summary>
    /// The polling loop: claim a task, execute it, report the outcome, repeat.
    /// </summary>
    /// <remarks>
    /// This type contains the whole behaviour of the agent and none of its plumbing. It talks
    /// to four abstractions and knows nothing about SQL Server, HTTP, Windows services or
    /// configuration files, which is what makes the loop testable in milliseconds.
    ///
    /// The governing constraint is that the platform's dequeue is destructive: GET
    /// /api/sync/next-task removes the task with no ack, no lease and no redelivery. From the
    /// moment a task is returned, this process holds the only copy of it. Every path out of
    /// ProcessTaskAsync therefore ends in a submitted result — success, validation failure or
    /// unexpected fault — and shutdown gets its own grace period for the same reason.
    /// </remarks>
    public sealed class SyncAgentWorker
    {
        /// <summary>
        /// What the platform is told when a task fails for a reason that is not its fault.
        /// </summary>
        /// <remarks>
        /// The exception's own message never travels. A SqlException message can name the
        /// server, the database and part of the statement; an IOException can name a local
        /// path. The type name is enough for the platform to triage, and the task id ties the
        /// report to the full detail in the agent's own log.
        /// </remarks>
        private const string FailureMessageFormat =
            "The agent could not complete this task ({0}). See the agent log for task {1}.";

        private readonly ISyncPlatformClient _platformClient;
        private readonly ISyncTaskDispatcher _dispatcher;
        private readonly ISyncTaskValidator _validator;
        private readonly IRetryExecutor _retryExecutor;
        private readonly IDelayProvider _delayProvider;
        private readonly ISystemClock _clock;
        private readonly SyncAgentOptions _options;
        private readonly ILogger<SyncAgentWorker> _logger;

        public SyncAgentWorker(
            ISyncPlatformClient platformClient,
            ISyncTaskDispatcher dispatcher,
            ISyncTaskValidator validator,
            IRetryExecutor retryExecutor,
            IDelayProvider delayProvider,
            ISystemClock clock,
            SyncAgentOptions options,
            ILogger<SyncAgentWorker> logger)
        {
            _platformClient = platformClient ?? throw new ArgumentNullException(nameof(platformClient));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _retryExecutor = retryExecutor ?? throw new ArgumentNullException(nameof(retryExecutor));
            _delayProvider = delayProvider ?? throw new ArgumentNullException(nameof(delayProvider));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Sync agent started. Polling {BaseUrl} every {PollInterval}. Supported task types: {TaskTypes}.",
                _options.PlatformBaseUrl,
                _options.PollInterval,
                string.Join(", ", _dispatcher.SupportedTaskTypes));

            var idleDelay = _options.PollInterval;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var task = await _retryExecutor
                        .ExecuteAsync(
                            "GetNextTask",
                            token => _platformClient.GetNextTaskAsync(token),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (task is null)
                    {
                        await _delayProvider.DelayAsync(idleDelay, cancellationToken).ConfigureAwait(false);
                        idleDelay = NextIdleDelay(idleDelay);
                        continue;
                    }

                    // Work appeared, so drop back to the fast cadence and poll again straight
                    // after this task rather than sleeping: a queue with several tasks in it
                    // should drain at the speed of the queries, not of the poll interval.
                    idleDelay = _options.PollInterval;

                    await ProcessTaskAsync(task, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Reaching here means even the retry policy gave up. The loop must not
                    // die — an unattended agent that exits on a platform restart needs a human
                    // — but it must not spin either, so the same backoff as an empty queue
                    // applies.
                    _logger.LogError(ex, "Polling cycle failed. Backing off for {Delay}.", idleDelay);

                    try
                    {
                        await _delayProvider.DelayAsync(idleDelay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    idleDelay = NextIdleDelay(idleDelay);
                }
            }

            _logger.LogInformation("Sync agent stopped.");
        }

        /// <summary>
        /// Executes one task and reports its outcome. Never throws for a task-level fault.
        /// </summary>
        private async Task ProcessTaskAsync(SyncTaskRequest task, CancellationToken cancellationToken)
        {
            var startedAt = _clock.UtcNow;
            SyncTaskResult result;
            var shuttingDown = false;

            try
            {
                _validator.EnsureValid(task);

                _logger.LogInformation(
                    "Executing task {TaskId} of type {TaskType}.",
                    task.TaskId,
                    task.TaskType);

                var payload = await _dispatcher.DispatchAsync(task, cancellationToken).ConfigureAwait(false);

                if (payload.WasTruncated)
                {
                    // Not an error the contract can express, and invisible from the platform
                    // side: a truncated extract looks exactly like a small one.
                    _logger.LogWarning(
                        "Task {TaskId} hit the {MaxRecords} record limit. The platform is receiving a partial extract.",
                        task.TaskId,
                        _options.MaxRecordsPerTask);
                }

                result = SyncTaskResult.Completed(task, payload.Records, _clock.UtcNow);

                _logger.LogInformation(
                    "Task {TaskId} completed with {RecordCount} records in {Elapsed}.",
                    task.TaskId,
                    result.RecordCount,
                    _clock.UtcNow - startedAt);
            }
            catch (SyncTaskValidationException ex)
            {
                // A bad task, not a broken agent. The message is written to be safe to send
                // back; the offending values go to the local log only.
                _logger.LogWarning(
                    "Rejected task {TaskId} of type {TaskType}: {Reason}",
                    task.TaskId,
                    task.TaskType,
                    ex.Message);

                result = SyncTaskResult.Failed(task, ex.Message, _clock.UtcNow);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown arrived mid-task. The platform has already forgotten this task, so
                // it is reported as failed rather than silently dropped, using the grace
                // period below.
                _logger.LogWarning(
                    "Task {TaskId} was interrupted by shutdown; reporting it as failed.",
                    task.TaskId);

                shuttingDown = true;
                result = SyncTaskResult.Failed(
                    task,
                    "The agent was shutting down before this task finished.",
                    _clock.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Task {TaskId} of type {TaskType} failed.",
                    task.TaskId,
                    task.TaskType);

                result = SyncTaskResult.Failed(
                    task,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        FailureMessageFormat,
                        ex.GetType().Name,
                        task.TaskId),
                    _clock.UtcNow);
            }

            await SubmitResultAsync(result, shuttingDown, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reports a result, retrying transient failures.
        /// </summary>
        /// <param name="shuttingDown">
        /// When true the caller's token is already cancelled, so submission runs on a fresh
        /// token bounded by the shutdown grace period instead. Without that, the report of an
        /// interrupted task would itself be cancelled and the task would disappear from both
        /// sides of the system.
        /// </param>
        private async Task SubmitResultAsync(
            SyncTaskResult result,
            bool shuttingDown,
            CancellationToken cancellationToken)
        {
            using (var graceTokenSource = shuttingDown
                       ? new CancellationTokenSource(_options.ShutdownGracePeriod)
                       : null)
            {
                var submitToken = graceTokenSource?.Token ?? cancellationToken;

                try
                {
                    await _retryExecutor
                        .ExecuteAsync(
                            "SubmitResult",
                            token => _platformClient.SubmitResultAsync(result, token),
                            submitToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The last line of defence. The platform cannot redeliver, so this task is
                    // now lost, and the log line is the only record that it ever existed.
                    // It carries everything needed to requeue it by hand.
                    _logger.LogError(
                        ex,
                        "Could not report the result of task {TaskId} ({TaskType}, status {Status}, {RecordCount} records). "
                        + "The platform does not redeliver, so this task is lost and must be requeued manually.",
                        result.TaskId,
                        result.TaskType,
                        result.Status,
                        result.RecordCount);
                }
            }
        }

        /// <summary>
        /// Doubles the idle wait up to the configured ceiling.
        /// </summary>
        /// <remarks>
        /// An agent left running against an empty queue overnight should cost close to
        /// nothing, and one polling a platform that is down should not hammer it while it
        /// tries to come back.
        /// </remarks>
        private TimeSpan NextIdleDelay(TimeSpan current)
        {
            var doubled = TimeSpan.FromTicks(current.Ticks * 2);

            return doubled > _options.MaxIdleBackoff ? _options.MaxIdleBackoff : doubled;
        }
    }
}

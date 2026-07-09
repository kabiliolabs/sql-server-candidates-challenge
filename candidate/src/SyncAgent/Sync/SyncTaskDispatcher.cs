using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;
using SyncAgent.Platform.Contracts;
using SyncAgent.Validation;

namespace SyncAgent.Sync;

/// <summary>
/// Routes a validated task to the one handler that declares its <c>taskType</c>.
/// </summary>
/// <remarks>
/// <para>
/// The routing table is built once from the injected handlers, so the set of registered handlers
/// <em>is</em> the task-type allowlist. A <c>taskType</c> never becomes a table name, a column
/// name or any other piece of SQL -- it is only ever a dictionary key. An unrecognised type is
/// reported as a failed task and no query is executed.
/// </para>
/// <para>
/// This class is also the failure boundary. A handler that throws must not take the worker down
/// with it, so every exception becomes a "failed" result and the polling loop carries on.
/// </para>
/// </remarks>
internal sealed class SyncTaskDispatcher : ISyncTaskDispatcher
{
    private readonly IReadOnlyDictionary<string, ISyncTaskHandler> _handlers;
    private readonly ITaskValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SyncTaskDispatcher> _logger;
    private readonly int _maxRecords;

    public SyncTaskDispatcher(
        IEnumerable<ISyncTaskHandler> handlers,
        ITaskValidator validator,
        IOptions<AgentOptions> agentOptions,
        TimeProvider timeProvider,
        ILogger<SyncTaskDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(agentOptions);

        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxRecords = agentOptions.Value.MaxRecords;
        _handlers = BuildRoutingTable(handlers);
    }

    public async Task<SyncResult?> DispatchAsync(SyncTask? task, CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(task);

        if (validation.Outcome == TaskValidationOutcome.Unaddressable)
        {
            // Posting a result would earn a 400 and tell the platform nothing it doesn't know.
            _logger.LogError("Discarding unusable task from the platform: {Reason}", validation.Error);
            return null;
        }

        // Guaranteed non-null: anything missing taskId/taskType was Unaddressable above.
        var taskId = task!.TaskId!;
        var taskType = task.TaskType!;
        var executedAt = _timeProvider.GetUtcNow().UtcDateTime;

        if (validation.Outcome == TaskValidationOutcome.Rejected)
        {
            // Non-null for every outcome but Valid: TaskValidationResult.Rejected requires a reason.
            var reason = validation.Error!;

            _logger.LogWarning("Rejected task {TaskId} ({TaskType}): {Reason}", taskId, taskType, reason);
            return SyncResult.Failed(taskId, taskType, reason, executedAt);
        }

        if (!_handlers.TryGetValue(taskType, out var handler))
        {
            _logger.LogWarning("No handler registered for task type {TaskType} (task {TaskId}).", taskType, taskId);
            return SyncResult.Failed(taskId, taskType, $"Unsupported task type: '{taskType}'.", executedAt);
        }

        var parameters = new SyncParameters(validation.ModifiedSince, _maxRecords);

        try
        {
            _logger.LogInformation(
                "Executing task {TaskId} ({TaskType}) for records modified since {ModifiedSince:o}, capped at {MaxRecords}.",
                taskId,
                taskType,
                parameters.ModifiedSince,
                parameters.MaxRecords);

            var data = await handler.ExecuteAsync(parameters, cancellationToken).ConfigureAwait(false);

            // Re-read the clock: executedAt should reflect when the query ran, not when we validated.
            return SyncResult.Completed(taskId, taskType, data, _timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is shutting down. This is not a task failure, so let it propagate rather
            // than reporting a spurious "failed" to the platform.
            throw;
        }
        catch (Exception ex)
        {
            // The exception detail stays local; only a category crosses the wire.
            _logger.LogError(ex, "Task {TaskId} ({TaskType}) failed.", taskId, taskType);
            return SyncResult.Failed(taskId, taskType, ErrorSanitizer.Sanitize(ex), _timeProvider.GetUtcNow().UtcDateTime);
        }
    }

    private static IReadOnlyDictionary<string, ISyncTaskHandler> BuildRoutingTable(IEnumerable<ISyncTaskHandler> handlers)
    {
        var table = new Dictionary<string, ISyncTaskHandler>(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlers)
        {
            if (!table.TryAdd(handler.TaskType, handler))
            {
                // Fail loudly at startup rather than silently letting one handler shadow another.
                throw new InvalidOperationException(
                    $"More than one {nameof(ISyncTaskHandler)} is registered for task type '{handler.TaskType}'.");
            }
        }

        return table;
    }
}

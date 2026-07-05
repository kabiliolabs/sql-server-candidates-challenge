namespace TaskExecutionService.Sync;

/// <summary>
/// Executes one kind of sync task. Adding support for a new task type is as
/// simple as implementing this interface and registering it in DI — no other
/// code needs to change (Open/Closed principle).
/// </summary>
public interface ISyncTaskExecutor
{
    /// <summary>The task type name this executor handles, e.g. "GetCustomers".</summary>
    string TaskType { get; }

    /// <summary>Runs the query and returns the rows to send back to the platform.</summary>
    Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken);
}

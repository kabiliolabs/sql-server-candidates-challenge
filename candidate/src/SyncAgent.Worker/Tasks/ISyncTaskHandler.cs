using SyncAgent.Worker.Platform;

namespace SyncAgent.Worker.Tasks;

/// <summary>
/// Executes the database query for one sync task type. Implementations are registered
/// in DI and picked up automatically by SyncTaskDispatcher via their TaskType - adding
/// a new task type means adding one new handler class and one registration line in
/// Program.cs, with nothing else in the dispatch pipeline changing.
/// </summary>
public interface ISyncTaskHandler
{
    SyncTaskType TaskType { get; }

    /// <summary>
    /// Runs the query and returns the result list (its concrete element type varies by
    /// task type - see Tasks/Models). Declared as object because the four task types
    /// genuinely have different result shapes; the JSON serializer resolves the actual
    /// type at runtime when this becomes the "data" field in step 4's POST /result.
    /// </summary>
    Task<object> ExecuteAsync(TaskParameters? parameters, CancellationToken cancellationToken);
}

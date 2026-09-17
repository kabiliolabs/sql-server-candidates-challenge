using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Abstractions;

/// <summary>Executes one sync task type end to end and produces the data to post back.
/// Adding a new task type means implementing this interface once and registering it in DI —
/// no changes to the dispatcher or the worker loop.</summary>
public interface ISyncTaskHandler
{
    /// <summary>Must be one of <see cref="TaskTypes"/>.</summary>
    string TaskType { get; }

    Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskRequest request, CancellationToken cancellationToken);
}

/// <summary>Validated parameters for a single task execution.</summary>
public sealed record SyncTaskRequest(string TaskId, DateTimeOffset ModifiedSince);

using SyncAgent.Contracts;

namespace SyncAgent.Handlers;

/// <summary>
/// Strategy interface for processing one sync task type end to end.
/// Implementations must always return a SyncResultDto with TaskId/TaskType
/// echoed back from the input task, on both success and failure.
/// </summary>
public interface ITaskHandler
{
    /// <summary>
    /// The taskType value (e.g. "GetCustomers") this handler processes.
    /// </summary>
    string TaskType { get; }

    Task<SyncResultDto> HandleAsync(SyncTaskDto task, CancellationToken cancellationToken);
}

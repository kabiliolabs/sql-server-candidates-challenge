namespace SyncAgent.Application.Abstractions;

/// <summary>
/// Executes one sync task type. Implementations are discovered through DI, so supporting a new
/// task type means adding an implementation and registering it - nothing else has to change.
/// </summary>
public interface ISyncTaskHandler
{
    /// <summary>The platform's task type string this handler answers to, e.g. "GetCustomers".</summary>
    string TaskType { get; }

    Task<SyncTaskExecutionResult> ExecuteAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken);
}

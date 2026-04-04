using SyncAgent.Core.Models;

namespace SyncAgent.Core.Interfaces;

/// <summary>
/// Strategy pattern: one implementation per task type.
/// Adding a new task type = create a new class implementing this interface and register it.
/// </summary>
public interface ISyncTaskHandler
{
    string TaskType { get; }
    Task<IEnumerable<object>> HandleAsync(TaskParameters parameters, CancellationToken cancellationToken);
}

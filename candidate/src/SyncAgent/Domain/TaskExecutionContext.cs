using SyncAgent.Contracts;

namespace SyncAgent.Domain;

public sealed class TaskExecutionContext
{
    public required SyncTaskContract Task { get; init; }
}

namespace SyncAgent.Core.Tasks;

/// <summary>A task returned by GET /api/sync/next-task.</summary>
public sealed class SyncTask
{
    public required string TaskId { get; init; }
    public required string TaskType { get; init; }
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

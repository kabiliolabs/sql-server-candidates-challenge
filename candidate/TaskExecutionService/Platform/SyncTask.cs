namespace TaskExecutionService.Platform;

/// <summary>
/// A unit of work handed out by the platform via GET /api/sync/next-task.
/// Mirrors the API contract; property names map to camelCase JSON through the
/// shared serializer options (see <c>SyncJson</c>).
/// </summary>
public sealed class SyncTask
{
    /// <summary>Unique task identifier (ULID). Must be echoed back in the result.</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>One of the known task type names, e.g. "GetCustomers".</summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>Task-specific parameters, e.g. { "modifiedSince": "2025-01-01T00:00:00Z" }.</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>UTC timestamp when the platform created the task.</summary>
    public DateTime CreatedAt { get; set; }
}

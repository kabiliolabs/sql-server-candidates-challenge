using System.Text.Json.Serialization;

namespace TaskExecutionService.Platform;

/// <summary>
/// Result of an executed task, posted to POST /api/sync/result. Mirrors the API
/// contract. Use the factory methods to guarantee the invariants the platform
/// validates (status/data/recordCount consistency).
/// </summary>
public sealed class SyncResult
{
    public const string StatusCompleted = "completed";
    public const string StatusFailed = "failed";

    public string TaskId { get; set; } = string.Empty;

    public string TaskType { get; set; } = string.Empty;

    /// <summary>Either "completed" or "failed" (case-sensitive per the contract).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Query results when completed; null when failed.</summary>
    public IReadOnlyList<object>? Data { get; set; }

    public int RecordCount { get; set; }

    public DateTime ExecutedAt { get; set; }

    /// <summary>Set only when failed; kept generic so no internal detail leaks to the platform.</summary>
    public string? ErrorMessage { get; set; }

    public static SyncResult Completed(string taskId, string taskType, IReadOnlyList<object> data, DateTime executedAtUtc) => new()
    {
        TaskId = taskId,
        TaskType = taskType,
        Status = StatusCompleted,
        Data = data,
        RecordCount = data.Count,
        ExecutedAt = executedAtUtc,
        ErrorMessage = null,
    };

    public static SyncResult Failed(string taskId, string taskType, string errorMessage, DateTime executedAtUtc) => new()
    {
        TaskId = taskId,
        TaskType = taskType,
        Status = StatusFailed,
        Data = null,
        RecordCount = 0,
        ExecutedAt = executedAtUtc,
        ErrorMessage = errorMessage,
    };
}

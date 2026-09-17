namespace SyncAgent.Core.Tasks;

/// <summary>The body posted to POST /api/sync/result.</summary>
public sealed class SyncResult
{
    public required string TaskId { get; init; }
    public required string TaskType { get; init; }
    public required string Status { get; init; }
    public IReadOnlyList<object>? Data { get; init; }
    public int RecordCount { get; init; }
    public DateTimeOffset ExecutedAt { get; init; }
    public string? ErrorMessage { get; init; }

    public static SyncResult Completed(string taskId, string taskType, IReadOnlyList<object> data, DateTimeOffset executedAt) =>
        new()
        {
            TaskId = taskId,
            TaskType = taskType,
            Status = SyncResultStatus.Completed,
            Data = data,
            RecordCount = data.Count,
            ExecutedAt = executedAt,
            ErrorMessage = null
        };

    public static SyncResult Failed(string taskId, string taskType, string errorMessage, DateTimeOffset executedAt) =>
        new()
        {
            TaskId = taskId,
            TaskType = taskType,
            Status = SyncResultStatus.Failed,
            Data = null,
            RecordCount = 0,
            ExecutedAt = executedAt,
            ErrorMessage = errorMessage
        };
}

public static class SyncResultStatus
{
    public const string Completed = "completed";
    public const string Failed = "failed";
}

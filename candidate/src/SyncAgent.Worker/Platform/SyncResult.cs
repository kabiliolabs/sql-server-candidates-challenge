using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Platform;

/// <summary>
/// The body of POST /api/sync/result, per docs/api-contract.md. Data is object, not a
/// generic type parameter, because the four task types genuinely have different result
/// shapes (see Tasks/Models) - System.Text.Json serializes it using its runtime type.
/// </summary>
public record SyncResult
{
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }

    [JsonPropertyName("taskType")]
    public required SyncTaskType TaskType { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("data")]
    public object? Data { get; init; }

    [JsonPropertyName("recordCount")]
    public int RecordCount { get; init; }

    [JsonPropertyName("executedAt")]
    public DateTimeOffset ExecutedAt { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    public static SyncResult Completed(SyncTask task, object data, int recordCount) => new()
    {
        TaskId = task.TaskId,
        TaskType = task.TaskType,
        Status = "completed",
        Data = data,
        RecordCount = recordCount,
        ExecutedAt = DateTimeOffset.UtcNow,
        ErrorMessage = null
    };

    public static SyncResult Failed(SyncTask task, string errorMessage) => new()
    {
        TaskId = task.TaskId,
        TaskType = task.TaskType,
        Status = "failed",
        Data = null,
        RecordCount = 0,
        ExecutedAt = DateTimeOffset.UtcNow,
        ErrorMessage = errorMessage
    };
}

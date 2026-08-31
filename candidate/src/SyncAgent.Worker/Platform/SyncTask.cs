using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Platform;

/// <summary>
/// A pending task returned by GET /api/sync/next-task, per docs/api-contract.md.
/// </summary>
public record SyncTask
{
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }

    [JsonPropertyName("taskType")]
    public required SyncTaskType TaskType { get; init; }

    [JsonPropertyName("parameters")]
    public TaskParameters? Parameters { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}

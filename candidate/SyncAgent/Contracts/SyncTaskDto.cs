using System.Text.Json.Serialization;

namespace SyncAgent.Contracts;

/// <summary>
/// A sync task received from GET /api/sync/next-task.
/// </summary>
public sealed class SyncTaskDto
{
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    [JsonPropertyName("taskType")]
    public required string TaskType { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

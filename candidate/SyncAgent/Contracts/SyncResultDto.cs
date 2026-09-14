using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyncAgent.Contracts;

/// <summary>
/// The result posted back to POST /api/sync/result for a processed sync task.
/// </summary>
public sealed class SyncResultDto
{
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    [JsonPropertyName("taskType")]
    public required string TaskType { get; set; }

    /// <summary>
    /// Either "completed" or "failed".
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }

    [JsonPropertyName("recordCount")]
    public int RecordCount { get; set; }

    [JsonPropertyName("executedAt")]
    public DateTime ExecutedAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

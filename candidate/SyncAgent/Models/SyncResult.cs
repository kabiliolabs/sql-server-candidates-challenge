using System.Text.Json.Serialization;

namespace SyncAgent.Models;

public class SyncResult
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("taskType")]
    public string TaskType { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("recordCount")]
    public int RecordCount { get; set; }

    [JsonPropertyName("executedAt")]
    public DateTime ExecutedAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    public static SyncResult Success(string taskId, string taskType, IReadOnlyCollection<object> data) =>
        new()
        {
            TaskId = taskId,
            TaskType = taskType,
            Status = "completed",
            Data = data,
            RecordCount = data.Count,
            ExecutedAt = DateTime.UtcNow
        };

    public static SyncResult Failure(string taskId, string taskType, string errorMessage) =>
        new()
        {
            TaskId = taskId,
            TaskType = taskType,
            Status = "failed",
            Data = null,
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };
}

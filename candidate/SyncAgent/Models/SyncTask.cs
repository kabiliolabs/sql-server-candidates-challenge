using System.Text.Json.Serialization;

namespace SyncAgent.Models;

public class SyncTask
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("taskType")]
    public string TaskType { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    public DateTime? GetModifiedSince()
    {
        if (Parameters.TryGetValue("modifiedSince", out var value) &&
            DateTime.TryParse(value, out var date))
            return date.ToUniversalTime();
        return null;
    }
}

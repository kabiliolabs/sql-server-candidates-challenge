using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyncTaskWorkerService.Models
{
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
    }
}

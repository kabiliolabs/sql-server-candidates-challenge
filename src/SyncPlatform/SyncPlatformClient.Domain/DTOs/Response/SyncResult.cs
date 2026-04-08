
using System.Text.Json.Serialization;

namespace SyncPlatformClient.Domain.DTOs
{
    public class SyncResult
    {
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; }

        [JsonPropertyName("taskType")]
        public string TaskType { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("recordCount")]
        public int RecordCount { get; set; }

        [JsonPropertyName("executedAt")]
        public DateTime ExecutedAt { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        public static SyncResult ToSyncResult(string taskId, string taskType, IReadOnlyCollection<object> data)
        {
            return new SyncResult()
            {
                Data = data,
                RecordCount = data.Count,
                ExecutedAt = DateTime.UtcNow,
                TaskId = taskId,
                TaskType = taskType,
                Status = "completed"
            };
        }
    }
}

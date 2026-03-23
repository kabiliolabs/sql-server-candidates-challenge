namespace SyncAgent.Core.Models;

public class SyncResult
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;  // "completed" | "failed"
    public int RecordCount { get; set; }
    public object? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

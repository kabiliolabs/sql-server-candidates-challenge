namespace SyncAgent.Core.Models;

public class SyncTask
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public TaskParameters Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

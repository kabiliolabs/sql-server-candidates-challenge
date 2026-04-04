namespace SyncAgent.Core.Models;

public class SyncResult
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Status { get; set; } = SyncResultStatus.Completed;
    public IEnumerable<object>? Data { get; set; }
    public int RecordCount { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public static SyncResult Success(SyncTask task, IEnumerable<object> data)
    {
        var items = data.ToList();
        return new SyncResult
        {
            TaskId = task.TaskId,
            TaskType = task.TaskType,
            Status = SyncResultStatus.Completed,
            Data = items,
            RecordCount = items.Count,
            ExecutedAt = DateTime.UtcNow
        };
    }

    public static SyncResult Failure(SyncTask task, string errorMessage) =>
        new()
        {
            TaskId = task.TaskId,
            TaskType = task.TaskType,
            Status = SyncResultStatus.Failed,
            Data = null,
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };
}

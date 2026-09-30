namespace SyncAgent.Core.Models
{
    public static class SyncStatus
    {
        public const string Completed = "completed";

        public const string Failed = "failed";
    }

    public sealed record SyncResult
    {
        public required string TaskId { get; init; }

        public required string TaskType { get; init; }

        public required string Status { get; init; }

        public IReadOnlyList<object>? Data { get; init; }

        public int RecordCount { get; init; }

        public DateTime ExecutedAt { get; init; }

        public string? ErrorMessage { get; init; }

        public static SyncResult Completed(SyncTask task, IReadOnlyList<object> data, DateTime executedAtUtc) => new()
        {
            TaskId = task.TaskId ?? string.Empty,
            TaskType = task.TaskType ?? string.Empty,
            Status = SyncStatus.Completed,
            Data = data,
            RecordCount = data.Count,
            ExecutedAt = executedAtUtc
        };

        public static SyncResult Failed(SyncTask task, string errorMessage, DateTime executedAtUtc) => new()
        {
            TaskId = task.TaskId ?? string.Empty,
            TaskType = task.TaskType ?? string.Empty,
            Status = SyncStatus.Failed,
            Data = null,
            RecordCount = 0,
            ExecutedAt = executedAtUtc,
            ErrorMessage = errorMessage
        };
    }
}
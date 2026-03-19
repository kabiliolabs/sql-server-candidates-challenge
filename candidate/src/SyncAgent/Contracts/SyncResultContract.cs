namespace SyncAgent.Contracts;

public sealed class SyncResultContract
{
    public string TaskId { get; init; } = string.Empty;
    public string TaskType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public object? Data { get; init; }
    public int RecordCount { get; init; }
    public DateTimeOffset ExecutedAt { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class SubmitResultResponseContract
{
    public bool Accepted { get; init; }
    public string? TaskId { get; init; }
    public string? Error { get; init; }
}

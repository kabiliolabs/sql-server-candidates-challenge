namespace SyncAgent.Contracts;

public sealed class SyncTaskContract
{
    public string TaskId { get; init; } = string.Empty;
    public string TaskType { get; init; } = string.Empty;
    public SyncTaskParametersContract Parameters { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class SyncTaskParametersContract
{
    public DateTimeOffset ModifiedSince { get; init; }
}

using System;

namespace SyncAgent.Models;

public sealed class SyncTask
{
    public string TaskId { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public SyncTaskParameters Parameters { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class SyncTaskParameters
{
    public DateTimeOffset? ModifiedSince { get; init; }
}
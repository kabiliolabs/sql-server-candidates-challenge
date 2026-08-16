using System;

namespace SyncAgent.Models;

public sealed record SyncTask
{
    public string TaskId { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public SyncTaskParameters Parameters { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record SyncTaskParameters
{
    public DateTimeOffset? ModifiedSince { get; init; }
}
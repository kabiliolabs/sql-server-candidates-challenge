using System;

namespace SyncAgent.Models;

public sealed record SyncResult
{
    public string TaskId { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public object? Data { get; init; }

    public int RecordCount { get; init; }

    public DateTimeOffset ExecutedAt { get; init; }

    public string? ErrorMessage { get; init; }
}
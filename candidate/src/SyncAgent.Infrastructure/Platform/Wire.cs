namespace SyncAgent.Infrastructure.Platform;

/// <summary>JSON wire shapes for the SyncPlatform API. Kept separate from the Core.Tasks
/// domain types so Core stays free of serialization concerns.</summary>
internal sealed record SyncTaskWire(
    string TaskId,
    string TaskType,
    IReadOnlyDictionary<string, string> Parameters,
    DateTimeOffset CreatedAt);

internal sealed record SyncResultWire(
    string TaskId,
    string TaskType,
    string Status,
    IReadOnlyList<object>? Data,
    int RecordCount,
    DateTimeOffset ExecutedAt,
    string? ErrorMessage);

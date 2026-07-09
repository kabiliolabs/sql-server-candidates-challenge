namespace SyncAgent.Platform.Contracts;

/// <summary>
/// The body posted to <c>POST /api/sync/result</c>.
/// </summary>
/// <remarks>
/// Construct through <see cref="Completed"/> or <see cref="Failed"/>. The platform rejects any
/// <c>status</c> that is not exactly "completed" or "failed", and rejects a missing
/// <c>taskId</c>/<c>taskType</c> with 400. Every setter is <c>private init</c> so those invariants
/// cannot be undone from outside -- not by an object initialiser, and not by a <c>with</c>
/// expression either. In particular <see cref="RecordCount"/> can never drift out of step with
/// <see cref="Data"/>.
/// </remarks>
public sealed record SyncResult
{
    public const string StatusCompleted = "completed";
    public const string StatusFailed = "failed";

    private SyncResult()
    {
    }

    public string TaskId { get; private init; } = string.Empty;

    public string TaskType { get; private init; } = string.Empty;

    public string Status { get; private init; } = string.Empty;

    /// <summary>Query results, or <see langword="null"/> when <see cref="Status"/> is "failed".</summary>
    public IReadOnlyList<object>? Data { get; private init; }

    public int RecordCount { get; private init; }

    public DateTime ExecutedAt { get; private init; }

    public string? ErrorMessage { get; private init; }

    public static SyncResult Completed(string taskId, string taskType, IReadOnlyList<object> data, DateTime executedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskType);
        ArgumentNullException.ThrowIfNull(data);

        return new SyncResult
        {
            TaskId = taskId,
            TaskType = taskType,
            Status = StatusCompleted,
            Data = data,
            RecordCount = data.Count,
            ExecutedAt = executedAt,
        };
    }

    /// <param name="errorMessage">
    /// Must already be sanitised. Raw exception text can carry the connection string, server
    /// names or a stack trace, none of which should cross the network to the platform.
    /// </param>
    public static SyncResult Failed(string taskId, string taskType, string errorMessage, DateTime executedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskType);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new SyncResult
        {
            TaskId = taskId,
            TaskType = taskType,
            Status = StatusFailed,
            Data = null,
            RecordCount = 0,
            ExecutedAt = executedAt,
            ErrorMessage = errorMessage,
        };
    }
}

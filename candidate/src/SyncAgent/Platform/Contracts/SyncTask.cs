namespace SyncAgent.Platform.Contracts;

/// <summary>
/// A unit of work handed to the agent by <c>GET /api/sync/next-task</c>.
/// </summary>
/// <remarks>
/// Every field is treated as untrusted input. <see cref="TaskType"/> is resolved against an
/// allowlist of handlers and never interpolated into SQL; <see cref="Parameters"/> values reach
/// the database only as bound <c>DbParameter</c>s.
/// </remarks>
public sealed record SyncTask
{
    public string? TaskId { get; init; }

    public string? TaskType { get; init; }

    /// <summary>
    /// Task-specific parameters. The platform models this as a string-to-string map,
    /// so values arrive unparsed and must be validated before use.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

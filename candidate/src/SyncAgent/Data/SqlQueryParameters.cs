namespace SyncAgent.Data;

/// <summary>
/// The bound parameters every sync query takes. Property names become <c>@ModifiedSince</c> and
/// <c>@MaxRecords</c>; both cross into SQL Server as <c>DbParameter</c>s, never as text.
/// </summary>
/// <param name="ModifiedSince">
/// Normalised to UTC by the validator, and passed as <see cref="DateTime"/> rather than
/// <see cref="DateTimeOffset"/> so it binds to a <c>datetime</c> column without a server-side
/// type conversion on every compared row.
/// </param>
/// <param name="MaxRecords">The row cap, from validated configuration rather than from the task.</param>
public sealed record SqlQueryParameters(DateTime ModifiedSince, int MaxRecords);

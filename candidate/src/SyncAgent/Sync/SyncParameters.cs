namespace SyncAgent.Sync;

/// <summary>
/// The validated, strongly-typed parameters handed to a handler.
/// </summary>
/// <remarks>
/// A handler never sees the raw <c>Dictionary&lt;string, string&gt;</c> off the wire. By the time
/// this record exists, <see cref="ModifiedSince"/> has been parsed from strict ISO-8601 and
/// <see cref="MaxRecords"/> has come from validated configuration, so neither can carry a payload.
/// </remarks>
/// <param name="ModifiedSince">Only records modified at or after this instant are returned.</param>
/// <param name="MaxRecords">Upper bound on the rows a handler may return.</param>
public sealed record SyncParameters(DateTimeOffset ModifiedSince, int MaxRecords);

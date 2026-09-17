namespace SyncAgent.Core.Abstractions;

/// <summary>Narrow, per-record-type read abstraction — one closed generic per task type
/// (ISyncQueryRepository&lt;CustomerRecord&gt;, ISyncQueryRepository&lt;ProductRecord&gt;, ...)
/// rather than one fat repository interface with a method per task. Core depends only on this
/// interface; Dapper/SQL specifics live entirely in the Infrastructure implementation, which is
/// what keeps Core free of I/O dependencies.</summary>
public interface ISyncQueryRepository<TRecord>
{
    Task<IReadOnlyList<TRecord>> GetModifiedSinceAsync(DateTimeOffset modifiedSince, int maxRows, CancellationToken cancellationToken);
}

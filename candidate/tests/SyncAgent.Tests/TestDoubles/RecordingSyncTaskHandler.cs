using SyncAgent.Sync;

namespace SyncAgent.Tests.TestDoubles;

/// <summary>
/// An <see cref="ISyncTaskHandler"/> that records what it was asked to do, and optionally
/// misbehaves. Deliberately hand-rolled: the dispatcher's contract is about which handler runs
/// and with what parameters, which reads better as recorded state than as mock verification.
/// </summary>
internal sealed class RecordingSyncTaskHandler(string taskType) : ISyncTaskHandler
{
    public string TaskType { get; } = taskType;

    public int ExecuteCount { get; private set; }

    public SyncParameters? LastParameters { get; private set; }

    /// <summary>Thrown from <see cref="ExecuteAsync"/> when set.</summary>
    public Exception? ThrowOnExecute { get; init; }

    /// <summary>Runs before the rows are returned. Used to advance a fake clock mid-execution.</summary>
    public Action? OnExecute { get; init; }

    public IReadOnlyList<object> Rows { get; init; } = [];

    public Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken)
    {
        ExecuteCount++;
        LastParameters = parameters;
        OnExecute?.Invoke();

        return ThrowOnExecute is not null
            ? Task.FromException<IReadOnlyList<object>>(ThrowOnExecute)
            : Task.FromResult(Rows);
    }
}

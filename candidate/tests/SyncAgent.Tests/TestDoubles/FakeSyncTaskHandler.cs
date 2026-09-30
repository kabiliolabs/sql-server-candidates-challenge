using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Models;

namespace SyncAgent.Tests.TestDoubles;

/// <summary>Hand-written fake: records calls and lets each test decide what ExecuteAsync does.</summary>
internal sealed class FakeSyncTaskHandler(
    string taskType,
    Func<SyncQuery, CancellationToken, Task<IReadOnlyList<object>>>? execute = null) : ISyncTaskHandler
{
    public string TaskType => taskType;

    public int Calls { get; private set; }

    public SyncQuery? LastQuery { get; private set; }

    public Task<IReadOnlyList<object>> ExecuteAsync(SyncQuery query, CancellationToken cancellationToken)
    {
        Calls++;
        LastQuery = query;
        return execute?.Invoke(query, cancellationToken)
               ?? Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
    }

    public static FakeSyncTaskHandler Returning(string taskType, params object[] records)
        => new(taskType, (_, _) => Task.FromResult<IReadOnlyList<object>>(records));

    public static FakeSyncTaskHandler Throwing(string taskType, Exception exception)
        => new(taskType, (_, _) => Task.FromException<IReadOnlyList<object>>(exception));
}

using SyncAgent.Platform.Contracts;
using SyncAgent.Sync;

namespace SyncAgent.Tests.TestDoubles;

internal sealed class FakeSyncTaskDispatcher : ISyncTaskDispatcher
{
    private readonly Func<SyncTask, SyncResult?> _dispatch;

    public FakeSyncTaskDispatcher(Func<SyncTask, SyncResult?>? dispatch = null) =>
        _dispatch = dispatch ?? (task => SyncResult.Completed(task.TaskId!, task.TaskType!, [], DateTime.UnixEpoch));

    public List<SyncTask> DispatchedTasks { get; } = [];

    public Task<SyncResult?> DispatchAsync(SyncTask? task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        DispatchedTasks.Add(task);
        return Task.FromResult(_dispatch(task));
    }
}

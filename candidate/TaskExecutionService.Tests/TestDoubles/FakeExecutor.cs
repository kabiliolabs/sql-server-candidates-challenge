using TaskExecutionService.Sync;

namespace TaskExecutionService.Tests.TestDoubles;

/// <summary>Configurable <see cref="ISyncTaskExecutor"/> for dispatcher tests.</summary>
public sealed class FakeExecutor : ISyncTaskExecutor
{
    private readonly Func<SyncParameters, IReadOnlyList<object>> _behavior;

    public FakeExecutor(string taskType, Func<SyncParameters, IReadOnlyList<object>> behavior)
    {
        TaskType = taskType;
        _behavior = behavior;
    }

    public string TaskType { get; }

    public bool WasCalled { get; private set; }

    public Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken)
    {
        WasCalled = true;
        return Task.FromResult(_behavior(parameters));
    }
}

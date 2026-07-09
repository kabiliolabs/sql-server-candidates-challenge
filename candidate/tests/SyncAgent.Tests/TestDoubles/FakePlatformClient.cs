using SyncAgent.Platform;
using SyncAgent.Platform.Contracts;

namespace SyncAgent.Tests.TestDoubles;

/// <summary>
/// A scripted <see cref="IPlatformClient"/> for driving the worker's polling loop.
/// Once the script runs out, every poll answers "queue empty", the same as a real idle platform.
/// </summary>
internal sealed class FakePlatformClient : IPlatformClient
{
    private readonly Queue<Func<SyncTask?>> _script = new();

    public int GetNextTaskCallCount { get; private set; }

    public List<SyncResult> PostedResults { get; } = [];

    public Exception? ThrowOnPost { get; set; }

    public FakePlatformClient Returns(SyncTask task)
    {
        _script.Enqueue(() => task);
        return this;
    }

    public FakePlatformClient Throws(Exception exception) => Throws(() => exception);

    /// <summary>Defers building the exception until the poll happens, so a test can cancel first.</summary>
    public FakePlatformClient Throws(Func<Exception> exceptionFactory)
    {
        _script.Enqueue(() => throw exceptionFactory());
        return this;
    }

    public Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetNextTaskCallCount++;

        return Task.FromResult(_script.Count > 0 ? _script.Dequeue()() : null);
    }

    public Task PostResultAsync(SyncResult result, CancellationToken cancellationToken)
    {
        if (ThrowOnPost is not null)
        {
            return Task.FromException(ThrowOnPost);
        }

        PostedResults.Add(result);
        return Task.CompletedTask;
    }
}

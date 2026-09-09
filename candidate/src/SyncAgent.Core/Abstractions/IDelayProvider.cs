using System;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Indirection over <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// Every wait in the agent — retry backoff, idle backoff between polls — goes through
    /// this. A test can then assert that a policy waited four times with a growing delay
    /// without the suite actually taking fifteen seconds, which is the difference between
    /// timing behaviour being tested and being hoped for.
    /// </remarks>
    public interface IDelayProvider
    {
        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }
}

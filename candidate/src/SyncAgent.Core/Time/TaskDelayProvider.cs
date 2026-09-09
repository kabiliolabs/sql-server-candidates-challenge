using System;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;

namespace SyncAgent.Core.Time
{
    /// <inheritdoc cref="IDelayProvider" />
    public sealed class TaskDelayProvider : IDelayProvider
    {
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return delay <= TimeSpan.Zero
                ? Task.FromResult(0)
                : Task.Delay(delay, cancellationToken);
        }
    }
}

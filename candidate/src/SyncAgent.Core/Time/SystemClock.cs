using System;
using SyncAgent.Core.Abstractions;

namespace SyncAgent.Core.Time
{
    /// <inheritdoc cref="ISystemClock" />
    public sealed class SystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}

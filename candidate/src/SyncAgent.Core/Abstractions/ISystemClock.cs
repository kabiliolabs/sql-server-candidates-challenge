using System;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Indirection over the system clock so time-dependent behaviour — the executedAt stamp,
    /// clock-skew tolerance, backoff delays — can be asserted in tests without sleeping.
    /// </summary>
    public interface ISystemClock
    {
        DateTimeOffset UtcNow { get; }
    }
}

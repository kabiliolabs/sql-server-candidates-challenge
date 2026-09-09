using System;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Decides whether a failure is worth trying again.
    /// </summary>
    /// <remarks>
    /// Kept separate from the retry policy so the two can vary independently: the policy owns
    /// how long to wait and how many times, and detectors own what "temporary" means for one
    /// kind of dependency. SQL Server and HTTP disagree about that, and neither answer belongs
    /// hard-coded inside a backoff loop.
    /// </remarks>
    public interface ITransientErrorDetector
    {
        bool IsTransient(Exception exception);
    }
}

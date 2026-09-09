using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Checks the shape of a task before the agent acts on it.
    /// </summary>
    public interface ISyncTaskValidator
    {
        /// <summary>
        /// Throws if the task is not well formed.
        /// </summary>
        /// <exception cref="Exceptions.SyncTaskValidationException">The task is unusable.</exception>
        void EnsureValid(SyncTaskRequest task);
    }
}

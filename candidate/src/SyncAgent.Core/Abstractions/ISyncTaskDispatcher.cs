using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Routes a task to the handler registered for its task type.
    /// </summary>
    public interface ISyncTaskDispatcher
    {
        /// <summary>
        /// The task types this agent can service. Reported at startup so an operator can see
        /// from the log which capabilities the deployed build actually has.
        /// </summary>
        IReadOnlyCollection<string> SupportedTaskTypes { get; }

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <exception cref="Exceptions.UnknownTaskTypeException">
        /// No handler is registered for the requested task type.
        /// </exception>
        Task<SyncPayload> DispatchAsync(SyncTaskRequest task, CancellationToken cancellationToken);
    }
}

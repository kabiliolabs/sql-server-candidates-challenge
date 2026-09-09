using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Executes one kind of sync task. This is the extension point of the agent.
    /// </summary>
    /// <remarks>
    /// Supporting a new task type means writing one implementation of this interface and
    /// registering it in the composition root. The dispatcher, the polling loop and the
    /// platform client stay closed for modification: they only ever see this abstraction.
    /// </remarks>
    public interface ISyncTaskHandler
    {
        /// <summary>
        /// The <c>taskType</c> value this handler answers to, for example <c>GetCustomers</c>.
        /// Routing matches it case-insensitively so a casing change upstream degrades into a
        /// still-working sync rather than a silent "unsupported task type".
        /// </summary>
        string TaskType { get; }

        Task<SyncPayload> ExecuteAsync(SyncTaskRequest task, CancellationToken cancellationToken);
    }
}

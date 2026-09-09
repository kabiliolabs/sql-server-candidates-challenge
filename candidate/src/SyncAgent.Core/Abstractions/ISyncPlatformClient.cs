using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// The agent's view of the central platform.
    /// </summary>
    public interface ISyncPlatformClient
    {
        /// <summary>
        /// Claims the next queued task, or returns <see langword="null"/> when the platform
        /// answers 204 because the queue is empty.
        /// </summary>
        /// <remarks>
        /// This is a destructive dequeue on the platform side: there is no ack, no lease and
        /// no redelivery. Once this call returns a task, the agent holds the only copy of it,
        /// which is why the worker always reports an outcome for whatever it claimed.
        /// </remarks>
        Task<SyncTaskRequest?> GetNextTaskAsync(CancellationToken cancellationToken);

        Task SubmitResultAsync(SyncTaskResult result, CancellationToken cancellationToken);
    }
}

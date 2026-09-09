using System;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Runs an operation, retrying it while the failure looks temporary.
    /// </summary>
    public interface IRetryExecutor
    {
        /// <param name="operationName">
        /// Used only for logging. It appears in every retry warning, so a log from a machine
        /// nobody can attach a debugger to still says which call was struggling.
        /// </param>
        Task<T> ExecuteAsync<T>(
            string operationName,
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken);

        Task ExecuteAsync(
            string operationName,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken);
    }
}

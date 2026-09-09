using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Handlers
{
    /// <summary>
    /// Base for every task type in the current contract: read a <c>modifiedSince</c> cutoff,
    /// run one bounded query, wrap the rows.
    /// </summary>
    /// <remarks>
    /// All four task types differ only in which query they run, so the cutoff parsing, the
    /// row limit and the truncation check live here once. A subclass is a task type name and
    /// a query call.
    ///
    /// This is a template method rather than a single generic handler configured with a
    /// delegate. A named class per task type keeps the type visible in stack traces and in
    /// the container registration, and leaves an obvious home for a future task type whose
    /// parameters are not just a cutoff date.
    /// </remarks>
    public abstract class ModifiedSinceSyncTaskHandler<TRecord> : ISyncTaskHandler
        where TRecord : class
    {
        private readonly ISyncTaskParameterReader _parameterReader;
        private readonly SyncAgentOptions _options;

        protected ModifiedSinceSyncTaskHandler(
            ISyncTaskParameterReader parameterReader,
            SyncAgentOptions options)
        {
            _parameterReader = parameterReader ?? throw new ArgumentNullException(nameof(parameterReader));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public abstract string TaskType { get; }

        public async Task<SyncPayload> ExecuteAsync(SyncTaskRequest task, CancellationToken cancellationToken)
        {
            // Parsing first means an invalid cutoff never reaches the database. The task
            // fails on a validation error rather than on a query that was never worth
            // running.
            var modifiedSince = _parameterReader.ReadModifiedSince(task);

            var records = await QueryAsync(
                    modifiedSince,
                    _options.MaxRecordsPerTask,
                    cancellationToken)
                .ConfigureAwait(false);

            return SyncPayload.From(records, _options.MaxRecordsPerTask);
        }

        /// <summary>
        /// Runs the task's query. Implementations must honour <paramref name="maxRecords"/>
        /// inside the SQL, not by trimming the result afterwards: the point of the cap is to
        /// stop the rows being materialised at all.
        /// </summary>
        protected abstract Task<IReadOnlyList<TRecord>> QueryAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models.Records;

namespace SyncAgent.Core.Handlers
{
    /// <summary>
    /// Handles the <c>GetOrders</c> task type.
    /// </summary>
    /// <remarks>
    /// The row cap applies to orders, not order lines, which matches the contract: the sample
    /// reports recordCount 1 for a single order carrying three lines.
    /// </remarks>
    public sealed class GetOrdersHandler : ModifiedSinceSyncTaskHandler<OrderRecord>
    {
        public const string TaskTypeName = "GetOrders";

        private readonly IOrderQuery _query;

        public GetOrdersHandler(
            IOrderQuery query,
            ISyncTaskParameterReader parameterReader,
            SyncAgentOptions options)
            : base(parameterReader, options)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public override string TaskType => TaskTypeName;

        protected override Task<IReadOnlyList<OrderRecord>> QueryAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken)
        {
            return _query.GetModifiedSinceAsync(modifiedSince, maxRecords, cancellationToken);
        }
    }
}

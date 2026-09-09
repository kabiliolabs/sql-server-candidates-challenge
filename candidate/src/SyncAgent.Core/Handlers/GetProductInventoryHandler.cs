using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models.Records;

namespace SyncAgent.Core.Handlers
{
    /// <summary>Handles the <c>GetProductInventory</c> task type.</summary>
    public sealed class GetProductInventoryHandler : ModifiedSinceSyncTaskHandler<ProductInventoryRecord>
    {
        public const string TaskTypeName = "GetProductInventory";

        private readonly IProductInventoryQuery _query;

        public GetProductInventoryHandler(
            IProductInventoryQuery query,
            ISyncTaskParameterReader parameterReader,
            SyncAgentOptions options)
            : base(parameterReader, options)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public override string TaskType => TaskTypeName;

        protected override Task<IReadOnlyList<ProductInventoryRecord>> QueryAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken)
        {
            return _query.GetModifiedSinceAsync(modifiedSince, maxRecords, cancellationToken);
        }
    }
}

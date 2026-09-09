using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models.Records;

namespace SyncAgent.Core.Handlers
{
    /// <summary>Handles the <c>GetProducts</c> task type.</summary>
    public sealed class GetProductsHandler : ModifiedSinceSyncTaskHandler<ProductRecord>
    {
        public const string TaskTypeName = "GetProducts";

        private readonly IProductQuery _query;

        public GetProductsHandler(
            IProductQuery query,
            ISyncTaskParameterReader parameterReader,
            SyncAgentOptions options)
            : base(parameterReader, options)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public override string TaskType => TaskTypeName;

        protected override Task<IReadOnlyList<ProductRecord>> QueryAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken)
        {
            return _query.GetModifiedSinceAsync(modifiedSince, maxRecords, cancellationToken);
        }
    }
}

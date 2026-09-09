using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models.Records;

namespace SyncAgent.Core.Handlers
{
    /// <summary>Handles the <c>GetCustomers</c> task type.</summary>
    public sealed class GetCustomersHandler : ModifiedSinceSyncTaskHandler<CustomerRecord>
    {
        public const string TaskTypeName = "GetCustomers";

        private readonly ICustomerQuery _query;

        public GetCustomersHandler(
            ICustomerQuery query,
            ISyncTaskParameterReader parameterReader,
            SyncAgentOptions options)
            : base(parameterReader, options)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public override string TaskType => TaskTypeName;

        protected override Task<IReadOnlyList<CustomerRecord>> QueryAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken)
        {
            return _query.GetModifiedSinceAsync(modifiedSince, maxRecords, cancellationToken);
        }
    }
}

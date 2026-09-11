using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SyncAgent.Contracts;
using SyncAgent.Domain;
using SyncAgent.Protocol;
using SyncAgent.Sql;

namespace SyncAgent.Handlers
{
    public sealed class GetProductInventoryHandler : ISyncTaskHandler
    {
        private const string Query = @"
SELECT TOP (@MaxRows) i.ProductID, p.Name AS ProductName, p.ProductNumber,
  l.Name AS LocationName, i.Shelf, i.Bin, i.Quantity, i.ModifiedDate
FROM Production.ProductInventory i
JOIN Production.Product p ON p.ProductID = i.ProductID
JOIN Production.Location l ON l.LocationID = i.LocationID
WHERE (@ModifiedSince IS NULL OR i.ModifiedDate >= @ModifiedSince)
ORDER BY i.ProductID";

        private readonly SqlConnectionFactory _connections;
        private readonly AgentOptions _options;
        private readonly ILogger<GetProductInventoryHandler> _logger;

        public string TaskType => "GetProductInventory";

        public GetProductInventoryHandler(
            SqlConnectionFactory connections,
            AgentOptions options,
            ILogger<GetProductInventoryHandler> logger)
        {
            _connections = connections;
            _options = options;
            _logger = logger;
        }

        public async Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken)
        {
            DateTime? modifiedSince = task.parameters?.modifiedSince;
            var rows = new List<object>();

            using (var conn = _connections.Create())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = Query;
                cmd.CommandTimeout = _options.CommandTimeoutSeconds;
                cmd.Parameters.Add(SqlParameterHelpers.ModifiedSince(modifiedSince));
                cmd.Parameters.Add(new SqlParameter("@MaxRows", SqlDbType.Int) { Value = _options.MaxRows });

                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    int productId = reader.GetOrdinal("ProductID");
                    int productName = reader.GetOrdinal("ProductName");
                    int productNumber = reader.GetOrdinal("ProductNumber");
                    int locationName = reader.GetOrdinal("LocationName");
                    int shelf = reader.GetOrdinal("Shelf");
                    int bin = reader.GetOrdinal("Bin");
                    int quantity = reader.GetOrdinal("Quantity");
                    int modifiedDate = reader.GetOrdinal("ModifiedDate");

                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new InventoryRow
                        {
                            productId = reader.GetInt32(productId),
                            productName = reader.GetString(productName),
                            productNumber = reader.GetString(productNumber),
                            locationName = reader.GetString(locationName),
                            shelf = reader.GetStringOrNull(shelf),
                            bin = reader.GetByte(bin),
                            quantity = reader.GetInt16(quantity),
                            modifiedDate = reader.GetDateTime(modifiedDate)
                        });
                    }
                }
            }

            _logger.LogInformation("GetProductInventory: {Count} rows (modifiedSince={ModifiedSince}).",
                rows.Count, modifiedSince.HasValue ? modifiedSince.Value.ToString("o") : "all");
            return SyncResult.Completed(task.taskId, task.taskType, rows, DateTime.UtcNow);
        }
    }
}
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
    public sealed class GetProductsHandler : ISyncTaskHandler
    {
        private const string Query = @"
SELECT TOP (@MaxRows) p.ProductID, p.Name, p.ProductNumber, p.Color,
  p.StandardCost, p.ListPrice, pc.Name AS Category, ps.Name AS Subcategory, p.ModifiedDate
FROM Production.Product p
LEFT JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
LEFT JOIN Production.ProductCategory pc ON pc.ProductCategoryID = ps.ProductCategoryID
WHERE (@ModifiedSince IS NULL OR p.ModifiedDate >= @ModifiedSince)
ORDER BY p.ProductID";

        private readonly SqlConnectionFactory _connections;
        private readonly AgentOptions _options;
        private readonly ILogger<GetProductsHandler> _logger;

        public string TaskType => "GetProducts";

        public GetProductsHandler(
            SqlConnectionFactory connections,
            AgentOptions options,
            ILogger<GetProductsHandler> logger)
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
                    int name = reader.GetOrdinal("Name");
                    int productNumber = reader.GetOrdinal("ProductNumber");
                    int color = reader.GetOrdinal("Color");
                    int standardCost = reader.GetOrdinal("StandardCost");
                    int listPrice = reader.GetOrdinal("ListPrice");
                    int category = reader.GetOrdinal("Category");
                    int subcategory = reader.GetOrdinal("Subcategory");
                    int modifiedDate = reader.GetOrdinal("ModifiedDate");

                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new ProductRow
                        {
                            productId = reader.GetInt32(productId),
                            name = reader.GetString(name),
                            productNumber = reader.GetString(productNumber),
                            color = reader.GetStringOrNull(color),
                            standardCost = reader.GetDecimal(standardCost),
                            listPrice = reader.GetDecimal(listPrice),
                            category = reader.GetStringOrNull(category),
                            subcategory = reader.GetStringOrNull(subcategory),
                            modifiedDate = reader.GetDateTime(modifiedDate)
                        });
                    }
                }
            }

            _logger.LogInformation("GetProducts: {Count} rows (modifiedSince={ModifiedSince}).",
                rows.Count, modifiedSince.HasValue ? modifiedSince.Value.ToString("o") : "all");
            return SyncResult.Completed(task.taskId, task.taskType, rows, DateTime.UtcNow);
        }
    }
}
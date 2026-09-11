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
    public sealed class GetOrdersHandler : ISyncTaskHandler
    {
        // Exactly two queries (headers + details), never one query per order (no N+1).
        private const string HeadersQuery = @"
SELECT TOP (@MaxRows) h.SalesOrderID, h.OrderDate, h.Status, h.TotalDue, c.AccountNumber,
  COALESCE(p.FirstName + N' ' + p.LastName, s.Name) AS CustomerName
FROM Sales.SalesOrderHeader h
JOIN Sales.Customer c ON c.CustomerID = h.CustomerID
LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
LEFT JOIN Sales.Store s ON s.BusinessEntityID = c.StoreID
WHERE (@ModifiedSince IS NULL OR h.ModifiedDate >= @ModifiedSince)
ORDER BY h.SalesOrderID";

        private const string DetailsQuery = @"
SELECT d.SalesOrderID, p.Name AS ProductName, p.ProductNumber,
  d.UnitPrice, d.OrderQty AS Quantity, d.LineTotal
FROM Sales.SalesOrderDetail d
JOIN Production.Product p ON p.ProductID = d.ProductID
WHERE d.SalesOrderID IN (
  SELECT TOP (@MaxRows) h.SalesOrderID
  FROM Sales.SalesOrderHeader h
  WHERE (@ModifiedSince IS NULL OR h.ModifiedDate >= @ModifiedSince)
  ORDER BY h.SalesOrderID
)
ORDER BY d.SalesOrderID, d.SalesOrderDetailID";

        private readonly SqlConnectionFactory _connections;
        private readonly AgentOptions _options;
        private readonly ILogger<GetOrdersHandler> _logger;

        public string TaskType => "GetOrders";

        public GetOrdersHandler(
            SqlConnectionFactory connections,
            AgentOptions options,
            ILogger<GetOrdersHandler> logger)
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
            using (var headers = conn.CreateCommand())
            {
                headers.CommandText = HeadersQuery;
                headers.CommandTimeout = _options.CommandTimeoutSeconds;
                headers.Parameters.Add(SqlParameterHelpers.ModifiedSince(modifiedSince));
                headers.Parameters.Add(new SqlParameter("@MaxRows", SqlDbType.Int) { Value = _options.MaxRows });

                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
                var byId = new Dictionary<int, OrderRow>();
                var ordered = new List<OrderRow>();
                using (SqlDataReader reader = await headers.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    int salesOrderId = reader.GetOrdinal("SalesOrderID");
                    int orderDate = reader.GetOrdinal("OrderDate");
                    int status = reader.GetOrdinal("Status");
                    int totalDue = reader.GetOrdinal("TotalDue");
                    int accountNumber = reader.GetOrdinal("AccountNumber");
                    int customerName = reader.GetOrdinal("CustomerName");

                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var order = new OrderRow
                        {
                            salesOrderId = reader.GetInt32(salesOrderId),
                            orderDate = reader.GetDateTime(orderDate),
                            status = reader.GetByte(status),
                            totalDue = reader.GetDecimal(totalDue),
                            accountNumber = reader.GetString(accountNumber),
                            customerName = reader.GetStringOrNull(customerName),
                            orderDetails = new List<OrderDetailRow>()
                        };
                        byId.Add(order.salesOrderId, order);
                        ordered.Add(order);
                    }
                }

                using (var details = conn.CreateCommand())
                {
                    details.CommandText = DetailsQuery;
                    details.CommandTimeout = _options.CommandTimeoutSeconds;
                    details.Parameters.Add(SqlParameterHelpers.ModifiedSince(modifiedSince));
                    details.Parameters.Add(new SqlParameter("@MaxRows", SqlDbType.Int) { Value = _options.MaxRows });

                    using (SqlDataReader reader = await details.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        int salesOrderId = reader.GetOrdinal("SalesOrderID");
                        int productName = reader.GetOrdinal("ProductName");
                        int productNumber = reader.GetOrdinal("ProductNumber");
                        int unitPrice = reader.GetOrdinal("UnitPrice");
                        int quantity = reader.GetOrdinal("Quantity");
                        int lineTotal = reader.GetOrdinal("LineTotal");

                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            OrderRow order;
                            if (byId.TryGetValue(reader.GetInt32(salesOrderId), out order))
                            {
                                order.orderDetails.Add(new OrderDetailRow
                                {
                                    productName = reader.GetString(productName),
                                    productNumber = reader.GetString(productNumber),
                                    unitPrice = reader.GetDecimal(unitPrice),
                                    quantity = reader.GetInt16(quantity),
                                    lineTotal = reader.GetDecimal(lineTotal)
                                });
                            }
                        }
                    }
                }

                foreach (OrderRow order in ordered)
                    rows.Add(order);
            }

            _logger.LogInformation("GetOrders: {Count} rows (modifiedSince={ModifiedSince}).",
                rows.Count, modifiedSince.HasValue ? modifiedSince.Value.ToString("o") : "all");
            return SyncResult.Completed(task.taskId, task.taskType, rows, DateTime.UtcNow);
        }
    }
}
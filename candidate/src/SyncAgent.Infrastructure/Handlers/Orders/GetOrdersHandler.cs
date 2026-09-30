using Dapper;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Handlers.Orders
{
    public sealed class GetOrdersHandler(
    IDbConnectionFactory connectionFactory,
    IOptions<SyncOptions> options,
    ILogger<GetOrdersHandler> logger) : ISyncTaskHandler
    {
        public string TaskType => SyncTaskTypes.GetOrders;

        private const string Sql = """
            SET NOCOUNT ON;

            DECLARE @OrderIds TABLE (SalesOrderID int PRIMARY KEY);

            INSERT INTO @OrderIds (SalesOrderID)
            SELECT TOP (@MaxRecords) h.SalesOrderID
            FROM Sales.SalesOrderHeader h
            WHERE h.ModifiedDate >= @ModifiedSince
            ORDER BY h.SalesOrderID;

            SELECT
                h.SalesOrderID AS SalesOrderId,
                h.OrderDate    AS OrderDate,
                h.Status       AS Status,
                COALESCE(NULLIF(CONCAT_WS(' ', p.FirstName, p.LastName), ''), s.Name) AS CustomerName,
                c.AccountNumber AS AccountNumber,
                h.TotalDue     AS TotalDue
            FROM @OrderIds ids
            JOIN Sales.SalesOrderHeader h ON h.SalesOrderID = ids.SalesOrderID
            JOIN Sales.Customer c         ON c.CustomerID = h.CustomerID
            LEFT JOIN Person.Person p     ON p.BusinessEntityID = c.PersonID
            LEFT JOIN Sales.Store s       ON s.BusinessEntityID = c.StoreID
            ORDER BY h.SalesOrderID;

            SELECT
                d.SalesOrderID   AS SalesOrderId,
                pr.Name          AS ProductName,
                pr.ProductNumber AS ProductNumber,
                d.UnitPrice      AS UnitPrice,
                d.OrderQty       AS Quantity,
                d.LineTotal      AS LineTotal
            FROM @OrderIds ids
            JOIN Sales.SalesOrderDetail d ON d.SalesOrderID = ids.SalesOrderID
            JOIN Production.Product pr    ON pr.ProductID = d.ProductID
            ORDER BY d.SalesOrderID, d.SalesOrderDetailID;
            """;

        public async Task<IReadOnlyList<object>> ExecuteAsync(SyncQuery query, CancellationToken cancellationToken)
        {
            var maxRecords = options.Value.MaxRecords;

            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

            var command = new CommandDefinition(
                Sql,
                new { ModifiedSince = query.ModifiedSinceUtc, MaxRecords = maxRecords },
                commandTimeout: options.Value.CommandTimeoutSeconds,
                cancellationToken: cancellationToken);

            await using var results = await connection.QueryMultipleAsync(command);
            var orders = (await results.ReadAsync<OrderRecord>()).AsList();
            var linesByOrder = (await results.ReadAsync<OrderDetailRow>()).ToLookup(l => l.SalesOrderId);

            foreach (var order in orders)
            {
                order.OrderDetails = linesByOrder[order.SalesOrderId]
                    .Select(l => new OrderDetailRecord
                    {
                        ProductName = l.ProductName,
                        ProductNumber = l.ProductNumber,
                        UnitPrice = l.UnitPrice,
                        Quantity = l.Quantity,
                        LineTotal = l.LineTotal
                    })
                    .ToList();
            }

            if (orders.Count >= maxRecords)
                logger.LogWarning("{TaskType} hit the MaxRecords limit ({MaxRecords}); results are truncated.",
                    TaskType, maxRecords);

            return orders;
        }
    }
}
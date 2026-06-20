
using Dapper;
using SyncTaskWorkerService.Data;
using System.Text.Json;

namespace SyncTaskWorkerService.SyncAgentTask.Handlers
{
    public class GetOrdersHandler(IDbConnection dbConnection) : ISyncTaskHandler
    {
        public string SyncName => "GetOrders";
        public async Task<IEnumerable<object>> HandleAsync(JsonElement parameters, CancellationToken ct)
        {
            DateTime modifiedSince = parameters.GetProperty("modifiedSince").GetDateTime();

            var connection = dbConnection.Create();

            var sql = @"SELECT 
                            o.SalesOrderID,
                            o.OrderDate,
                            o.Status,
                            per.FirstName,
                            per.LastName,
                            o.AccountNumber,
                            o.TotalDue,
                            p.Name           AS ProductName,
                            p.ProductNumber,
                            d.UnitPrice,
                            d.OrderQty,
                            d.LineTotal
                        FROM Sales.SalesOrderHeader o
                        JOIN Sales.SalesOrderDetail d   ON o.SalesOrderID = d.SalesOrderID
                        JOIN Production.Product p       ON d.ProductID = p.ProductID
                        JOIN Sales.Customer c           ON o.CustomerID = c.CustomerID
                        JOIN Person.Person per          ON c.PersonID = per.BusinessEntityID
                        WHERE o.ModifiedDate >= @ModifiedDate";

            var flat = await connection.QueryAsync(sql, new { ModifiedDate = modifiedSince });

            var orders = flat
                .GroupBy(r => (int)r.SalesOrderID)
                .Select(g =>
                {
                    var first = g.First();
                    return new
                    {
                        salesOrderId = (int)first.SalesOrderID,
                        orderDate = (DateTime)first.OrderDate,
                        status = (byte)first.Status,
                        customerName = $"{first.FirstName} {first.LastName}",
                        accountNumber = (string)first.AccountNumber,
                        totalDue = (decimal)first.TotalDue,
                        orderDetails = g.Select(d => new
                        {
                            productName = (string)d.ProductName,
                            productNumber = (string)d.ProductNumber,
                            unitPrice = (decimal)d.UnitPrice,
                            quantity = (int)d.OrderQty,
                            lineTotal = (decimal)d.LineTotal
                        }).ToList()
                    };
                })
                .ToList();

            return orders;
        }
    }
}

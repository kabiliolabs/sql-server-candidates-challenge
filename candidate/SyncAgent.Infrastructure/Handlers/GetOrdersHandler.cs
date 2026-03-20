using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.DTOs;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Handlers;

public class GetOrdersHandler : ISyncTaskHandler
{
    public string TaskType => "GetOrders";

    private readonly string _connectionString;

    public GetOrdersHandler(IOptions<SyncAgentOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IEnumerable<object>> HandleAsync(
        TaskParameters parameters,
        CancellationToken cancellationToken)
    {
        // Two-query approach: fetch headers first, then details for those order IDs.
        // This avoids a cartesian explosion from joining details inline and is easier to map.

        const string headersSql = """
            SELECT
                soh.SalesOrderID,
                soh.OrderDate,
                soh.Status,
                soh.AccountNumber,
                soh.TotalDue,
                p.FirstName + ' ' + p.LastName AS CustomerName
            FROM Sales.SalesOrderHeader soh
            INNER JOIN Sales.Customer c
                ON soh.CustomerID = c.CustomerID
            INNER JOIN Person.Person p
                ON c.PersonID = p.BusinessEntityID
            WHERE (@ModifiedSince IS NULL OR soh.ModifiedDate >= @ModifiedSince)
            """;

        // SalesOrderDetail.ProductID FK points to SpecialOfferProduct, not directly to Product.
        // Must join through SpecialOfferProduct to correctly resolve the product.
        // Using the same @ModifiedSince filter via a JOIN to SalesOrderHeader avoids
        // passing thousands of order IDs as parameters (SQL Server limit: 2100).
        const string detailsSql = """
            SELECT
                sod.SalesOrderID,
                pr.Name          AS ProductName,
                pr.ProductNumber,
                sod.UnitPrice,
                sod.OrderQty     AS Quantity,
                sod.LineTotal
            FROM Sales.SalesOrderDetail sod
            INNER JOIN Sales.SalesOrderHeader soh
                ON sod.SalesOrderID = soh.SalesOrderID
            INNER JOIN Sales.SpecialOfferProduct sop
                ON sod.ProductID = sop.ProductID AND sod.SpecialOfferID = sop.SpecialOfferID
            INNER JOIN Production.Product pr
                ON sop.ProductID = pr.ProductID
            WHERE (@ModifiedSince IS NULL OR soh.ModifiedDate >= @ModifiedSince)
            """;

        await using var connection = new SqlConnection(_connectionString);

        var headers = (await connection.QueryAsync<OrderDto>(
            headersSql,
            new { parameters.ModifiedSince },
            commandTimeout: 60)).ToList();

        if (headers.Count == 0)
            return [];

        var details = await connection.QueryAsync<OrderDetailRow>(
            detailsSql,
            new { parameters.ModifiedSince },
            commandTimeout: 60);

        // Group details by order and attach them
        var detailsByOrder = details
            .GroupBy(d => d.SalesOrderId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => new OrderDetailDto
                {
                    ProductName = d.ProductName,
                    ProductNumber = d.ProductNumber,
                    UnitPrice = d.UnitPrice,
                    Quantity = d.Quantity,
                    LineTotal = d.LineTotal
                }).ToList());

        foreach (var order in headers)
        {
            if (detailsByOrder.TryGetValue(order.SalesOrderId, out var orderDetails))
                order.OrderDetails = orderDetails;
        }

        return headers.Cast<object>();
    }

    // Internal flat row Dapper maps to before assembling the nested DTO
    private sealed class OrderDetailRow
    {
        public int SalesOrderId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductNumber { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
    }
}

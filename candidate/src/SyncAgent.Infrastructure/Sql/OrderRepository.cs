using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Application.Abstractions;
using SyncAgent.Domain.Models;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Sql;

public sealed class OrderRepository(IOptions<DatabaseOptions> options, ILogger<OrderRepository> logger)
    : IOrderRepository
{
    // Headers and lines are read in one pass and grouped in memory, rather than querying the lines
    // per order. AccountNumber comes from Sales.Customer: SalesOrderHeader has its own, differently
    // formatted AccountNumber column, and the contract's sample uses the customer's.
    private const string Sql = """
        SELECT
            soh.SalesOrderID,
            soh.OrderDate,
            soh.Status,
            p.FirstName,
            p.LastName,
            c.AccountNumber,
            soh.TotalDue,
            prod.Name AS ProductName,
            prod.ProductNumber,
            sod.UnitPrice,
            sod.OrderQty,
            sod.LineTotal
        FROM Sales.SalesOrderHeader soh
        INNER JOIN Sales.Customer c ON soh.CustomerID = c.CustomerID
        INNER JOIN Person.Person p ON c.PersonID = p.BusinessEntityID
        INNER JOIN Sales.SalesOrderDetail sod ON soh.SalesOrderID = sod.SalesOrderID
        INNER JOIN Production.Product prod ON sod.ProductID = prod.ProductID
        WHERE soh.ModifiedDate >= @ModifiedSince
        ORDER BY soh.SalesOrderID, sod.SalesOrderDetailID
        """;

    private readonly DatabaseOptions _options = options.Value;

    public async Task<List<Order>> GetOrdersAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var orders = new Dictionary<int, Order>();

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(Sql, connection) { CommandTimeout = _options.CommandTimeoutSeconds };
        command.Parameters.Add("@ModifiedSince", SqlDbType.DateTime).Value = modifiedSince;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var salesOrderId = reader.GetInt32(0);

            if (!orders.TryGetValue(salesOrderId, out var order))
            {
                order = new Order
                {
                    SalesOrderId = salesOrderId,
                    OrderDate = reader.GetUtcDateTime(1),
                    Status = reader.GetByte(2),
                    CustomerName = $"{reader.GetString(3)} {reader.GetString(4)}",
                    AccountNumber = reader.GetString(5),
                    TotalDue = reader.GetDecimal(6),
                    OrderDetails = []
                };
                orders[salesOrderId] = order;
            }

            order.OrderDetails.Add(new OrderLine
            {
                ProductName = reader.GetString(7),
                ProductNumber = reader.GetString(8),
                UnitPrice = reader.GetDecimal(9),
                Quantity = reader.GetInt16(10),
                LineTotal = reader.GetDecimal(11)
            });
        }

        logger.LogInformation("Retrieved {OrderCount} orders", orders.Count);
        return [.. orders.Values];
    }
}

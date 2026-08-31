using Dapper;
using SyncAgent.Worker.Data;
using SyncAgent.Worker.Platform;
using SyncAgent.Worker.Tasks.Models;

namespace SyncAgent.Worker.Tasks;

public class GetOrdersTaskHandler(ISqlConnectionFactory connectionFactory) : ISyncTaskHandler
{
    public SyncTaskType TaskType => SyncTaskType.GetOrders;

    // One row per order line (header fields repeated per line) - grouped by
    // SalesOrderId in C# below into the nested OrderResult.OrderDetails shape the
    // platform expects. A single JOIN + in-memory GroupBy is simpler than a second
    // round trip, and AdventureWorks order volumes make that an easy trade-off.
    private const string Sql = """
        SELECT
            soh.SalesOrderID AS SalesOrderId,
            soh.OrderDate,
            soh.Status,
            p.FirstName + ' ' + p.LastName AS CustomerName,
            c.AccountNumber,
            soh.TotalDue,
            prod.Name AS ProductName,
            prod.ProductNumber,
            sod.UnitPrice,
            sod.OrderQty AS Quantity,
            sod.LineTotal
        FROM Sales.SalesOrderHeader soh
        INNER JOIN Sales.Customer c ON c.CustomerID = soh.CustomerID
        INNER JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
        INNER JOIN Sales.SalesOrderDetail sod ON sod.SalesOrderID = soh.SalesOrderID
        INNER JOIN Production.Product prod ON prod.ProductID = sod.ProductID
        WHERE (@ModifiedSince IS NULL OR soh.ModifiedDate >= @ModifiedSince)
        ORDER BY soh.SalesOrderID, sod.SalesOrderDetailID
        """;

    private record OrderLineRow
    {
        public int SalesOrderId { get; init; }
        public DateTime OrderDate { get; init; }
        public byte Status { get; init; }
        public string? CustomerName { get; init; }
        public string? AccountNumber { get; init; }
        public decimal TotalDue { get; init; }
        public string? ProductName { get; init; }
        public string? ProductNumber { get; init; }
        public decimal UnitPrice { get; init; }
        public int Quantity { get; init; }
        public decimal LineTotal { get; init; }
    }

    public async Task<object> ExecuteAsync(TaskParameters? parameters, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            Sql,
            new { parameters?.ModifiedSince },
            cancellationToken: cancellationToken);

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<OrderLineRow>(command);

        return rows
            .GroupBy(row => row.SalesOrderId)
            .Select(group =>
            {
                var header = group.First();
                return new OrderResult
                {
                    SalesOrderId = header.SalesOrderId,
                    OrderDate = header.OrderDate,
                    Status = header.Status,
                    CustomerName = header.CustomerName,
                    AccountNumber = header.AccountNumber,
                    TotalDue = header.TotalDue,
                    OrderDetails = [.. group.Select(line => new OrderDetailResult
                    {
                        ProductName = line.ProductName,
                        ProductNumber = line.ProductNumber,
                        UnitPrice = line.UnitPrice,
                        Quantity = line.Quantity,
                        LineTotal = line.LineTotal
                    })]
                };
            })
            .ToList();
    }
}

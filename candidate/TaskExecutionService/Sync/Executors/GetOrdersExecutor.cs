using Dapper;
using TaskExecutionService.Data;
using TaskExecutionService.Models;

namespace TaskExecutionService.Sync.Executors;

/// <summary>
/// Returns sales orders (headers with nested line items) modified since the given
/// date. Runs two set-based queries filtered by the same predicate, then groups
/// the details under their orders in memory — avoiding an N+1 query pattern.
/// The customer name comes from the linked Person, falling back to the Store name
/// (a customer may be linked to both).
/// </summary>
public sealed class GetOrdersExecutor : ISyncTaskExecutor
{
    public const string Type = "GetOrders";

    private const string HeadersSql = """
        SELECT
            soh.SalesOrderID AS SalesOrderId,
            soh.OrderDate    AS OrderDate,
            soh.Status       AS Status,
            COALESCE(p.FirstName + ' ' + p.LastName, s.Name) AS CustomerName,
            c.AccountNumber  AS AccountNumber,
            soh.TotalDue     AS TotalDue
        FROM Sales.SalesOrderHeader soh
        INNER JOIN Sales.Customer c ON c.CustomerID = soh.CustomerID
        LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
        LEFT JOIN Sales.Store s ON s.BusinessEntityID = c.StoreID
        WHERE soh.ModifiedDate >= @modifiedSince
        ORDER BY soh.SalesOrderID;
        """;

    private const string DetailsSql = """
        SELECT
            sod.SalesOrderID AS SalesOrderId,
            p.Name           AS ProductName,
            p.ProductNumber  AS ProductNumber,
            sod.UnitPrice    AS UnitPrice,
            sod.OrderQty     AS Quantity,
            sod.LineTotal    AS LineTotal
        FROM Sales.SalesOrderDetail sod
        INNER JOIN Sales.SalesOrderHeader soh ON soh.SalesOrderID = sod.SalesOrderID
        INNER JOIN Production.Product p ON p.ProductID = sod.ProductID
        WHERE soh.ModifiedDate >= @modifiedSince
        ORDER BY sod.SalesOrderID, sod.SalesOrderDetailID;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public GetOrdersExecutor(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public string TaskType => Type;

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken)
    {
        var args = new { modifiedSince = parameters.ModifiedSince };
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var headers = (await connection.QueryAsync<OrderDto>(new CommandDefinition(
            HeadersSql, args, commandTimeout: _connectionFactory.CommandTimeoutSeconds, cancellationToken: cancellationToken)))
            .ToArray();

        var details = await connection.QueryAsync<OrderDetailDto>(new CommandDefinition(
            DetailsSql, args, commandTimeout: _connectionFactory.CommandTimeoutSeconds, cancellationToken: cancellationToken));

        var detailsByOrder = details
            .GroupBy(d => d.SalesOrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderDetailDto>)g.ToArray());

        foreach (var header in headers)
        {
            header.OrderDetails = detailsByOrder.TryGetValue(header.SalesOrderId, out var lines)
                ? lines
                : Array.Empty<OrderDetailDto>();
        }

        return headers;
    }
}

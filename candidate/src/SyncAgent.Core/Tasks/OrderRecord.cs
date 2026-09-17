namespace SyncAgent.Core.Tasks;

public sealed record OrderRecord(
    int SalesOrderId,
    DateTimeOffset OrderDate,
    byte Status,
    string? CustomerName,
    string? AccountNumber,
    decimal TotalDue,
    IReadOnlyList<OrderDetailRecord> OrderDetails);

public sealed record OrderDetailRecord(
    string ProductName,
    string ProductNumber,
    decimal UnitPrice,
    short Quantity,
    decimal LineTotal);

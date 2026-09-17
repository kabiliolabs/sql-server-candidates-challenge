namespace SyncAgent.Core.Tasks;

public sealed record ProductInventoryRecord(
    int ProductId,
    string ProductName,
    string ProductNumber,
    string? LocationName,
    string? Shelf,
    byte Bin,
    short Quantity,
    DateTimeOffset ModifiedDate);

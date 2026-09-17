namespace SyncAgent.Infrastructure.Data;

/// <summary>See ProductRow — same DateTime-vs-DateTimeOffset reasoning applies here.</summary>
internal sealed record ProductInventoryRow(
    int ProductId,
    string ProductName,
    string ProductNumber,
    string? LocationName,
    string? Shelf,
    byte Bin,
    short Quantity,
    DateTime ModifiedDate);

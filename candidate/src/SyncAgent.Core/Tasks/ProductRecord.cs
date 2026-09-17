namespace SyncAgent.Core.Tasks;

public sealed record ProductRecord(
    int ProductId,
    string Name,
    string ProductNumber,
    string? Color,
    decimal? StandardCost,
    decimal? ListPrice,
    string? Category,
    string? Subcategory,
    DateTimeOffset ModifiedDate);

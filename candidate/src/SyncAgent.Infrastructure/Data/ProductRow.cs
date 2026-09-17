namespace SyncAgent.Infrastructure.Data;

/// <summary>Exact shape of ProductQueries.SelectModifiedSince. ModifiedDate is DateTime here
/// (matching the SQL `datetime` column) rather than DateTimeOffset — Dapper's constructor
/// materialization can't bridge that type gap, so the conversion happens explicitly in
/// ProductRepository.MapToRecord instead.</summary>
internal sealed record ProductRow(
    int ProductId,
    string Name,
    string ProductNumber,
    string? Color,
    decimal? StandardCost,
    decimal? ListPrice,
    string? Category,
    string? Subcategory,
    DateTime ModifiedDate);

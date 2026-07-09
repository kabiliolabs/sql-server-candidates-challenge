namespace SyncAgent.Dto;

/// <summary>One row of <c>GetProductInventory</c>.</summary>
/// <remarks>
/// The primary key of <c>Production.ProductInventory</c> is (ProductID, LocationID), so
/// <see cref="ProductId"/> is deliberately not unique across the result set: a product stocked in
/// four locations is four rows. <see cref="Bin"/> (tinyint) and <see cref="Quantity"/> (smallint)
/// are cast to int in SQL rather than relying on an implicit numeric conversion in the mapper.
/// </remarks>
public sealed record InventoryDto
{
    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductNumber { get; init; } = string.Empty;

    public string LocationName { get; init; } = string.Empty;

    public string? Shelf { get; init; }

    public int Bin { get; init; }

    public int Quantity { get; init; }

    public DateTime ModifiedDate { get; init; }
}

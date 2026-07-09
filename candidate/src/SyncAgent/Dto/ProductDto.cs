namespace SyncAgent.Dto;

/// <summary>One row of <c>GetProducts</c>.</summary>
/// <remarks>
/// 209 of 504 products have no subcategory, so <see cref="Category"/> and <see cref="Subcategory"/>
/// are nullable: an INNER JOIN to the category tables would silently drop 41% of the catalogue.
/// Money columns map to <see cref="decimal"/>, never <c>double</c>.
/// </remarks>
public sealed record ProductDto
{
    public int ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ProductNumber { get; init; } = string.Empty;

    /// <summary>Null for products with no colour. Must stay null, not "".</summary>
    public string? Color { get; init; }

    public decimal StandardCost { get; init; }

    public decimal ListPrice { get; init; }

    public string? Category { get; init; }

    public string? Subcategory { get; init; }

    public DateTime ModifiedDate { get; init; }
}

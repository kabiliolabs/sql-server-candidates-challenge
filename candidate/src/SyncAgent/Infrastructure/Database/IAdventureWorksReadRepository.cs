namespace SyncAgent.Infrastructure.Database;

public interface IAdventureWorksReadRepository
{
    Task<IReadOnlyCollection<CustomerSyncDto>> GetCustomersAsync(DateTimeOffset modifiedSince, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductSyncDto>> GetProductsAsync(DateTimeOffset modifiedSince, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductInventorySyncDto>> GetProductInventoryAsync(DateTimeOffset modifiedSince, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OrderSyncDto>> GetOrdersAsync(DateTimeOffset modifiedSince, CancellationToken cancellationToken);
}

public sealed class CustomerSyncDto
{
    public int CustomerId { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? EmailAddress { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? StateProvince { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryRegion { get; init; }
}

public sealed class ProductSyncDto
{
    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ProductNumber { get; init; } = string.Empty;
    public string? Color { get; init; }
    public decimal StandardCost { get; init; }
    public decimal ListPrice { get; init; }
    public string? Category { get; init; }
    public string? Subcategory { get; init; }
    public DateTime ModifiedDate { get; init; }
}

public sealed class ProductInventorySyncDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ProductNumber { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string Shelf { get; init; } = string.Empty;
    public short Bin { get; init; }
    public int Quantity { get; init; }
    public DateTime ModifiedDate { get; init; }
}

public sealed class OrderSyncDto
{
    public int SalesOrderId { get; init; }
    public DateTime OrderDate { get; init; }
    public byte Status { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public decimal TotalDue { get; init; }
    public IReadOnlyCollection<OrderDetailSyncDto> OrderDetails { get; init; } = Array.Empty<OrderDetailSyncDto>();
}

public sealed class OrderDetailSyncDto
{
    public string ProductName { get; init; } = string.Empty;
    public string ProductNumber { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public short Quantity { get; init; }
    public decimal LineTotal { get; init; }
}

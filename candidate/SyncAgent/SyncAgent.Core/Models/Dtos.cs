namespace SyncAgent.Core.Models;

public record CustomerDto(
    int CustomerId,
    string FirstName,
    string LastName,
    int EmailPromotion,
    string? EmailAddress,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateProvince,
    string? CountryRegion,
    string? PostalCode,
    DateTime ModifiedDate
);

public record ProductDto(
    int ProductId,
    string Name,
    string ProductNumber,
    string? Color,
    decimal StandardCost,
    decimal ListPrice,
    string? Size,
    decimal? Weight,
    string? Category,
    string? SubCategory,
    string? ProductLine,
    string? Style,
    DateTime SellStartDate,
    DateTime? SellEndDate,
    DateTime? DiscontinuedDate,
    DateTime ModifiedDate
);

public record OrderLineItemDto(
    int SalesOrderDetailId,
    int ProductId,
    string ProductName,
    short OrderQty,
    decimal UnitPrice,
    decimal UnitPriceDiscount,
    decimal LineTotal
);

public record OrderDto(
    int SalesOrderId,
    DateTime OrderDate,
    DateTime DueDate,
    DateTime? ShipDate,
    byte Status,
    string? PurchaseOrderNumber,
    string? AccountNumber,
    int CustomerId,
    string CustomerFirstName,
    string CustomerLastName,
    decimal SubTotal,
    decimal TaxAmt,
    decimal Freight,
    decimal TotalDue,
    DateTime ModifiedDate,
    List<OrderLineItemDto> LineItems
);

public record ProductInventoryDto(
    int ProductId,
    string ProductName,
    short LocationId,
    string LocationName,
    string Shelf,
    byte Bin,
    short Quantity,
    DateTime ModifiedDate
);

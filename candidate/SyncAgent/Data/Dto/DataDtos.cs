namespace SyncAgent.Data.Dto;

public class CustomerDto
{
    public int CustomerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? EmailAddress { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryRegion { get; set; }
}

public class ProductDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string? Color { get; set; }
    public decimal StandardCost { get; set; }
    public decimal ListPrice { get; set; }
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
    public DateTime ModifiedDate { get; set; }
}

/// <summary>Flat row returned from SQL before grouping into order + orderDetails.</summary>
public class OrderFlatRow
{
    public int SalesOrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public byte Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal TotalDue { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public short Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class ProductInventoryDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public byte Bin { get; set; }
    public short Quantity { get; set; }
    public DateTime ModifiedDate { get; set; }
}

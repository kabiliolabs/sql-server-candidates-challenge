using System;
using System.Collections.Generic;

namespace SyncAgent.Models;

public sealed class OrderDto
{
    public int SalesOrderId { get; init; }
    public DateTime OrderDate { get; set; }
    public byte Status { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public decimal TotalDue { get; init; }

    public List<OrderDetailDto> OrderDetails { get; set; } = [];
}

public sealed class OrderDetailDto
{
    public string ProductName { get; init; } = string.Empty;
    public string ProductNumber { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public short Quantity { get; init; }
    public decimal LineTotal { get; init; }
}
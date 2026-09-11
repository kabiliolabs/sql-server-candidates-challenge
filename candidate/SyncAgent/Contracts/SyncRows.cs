using System;
using System.Collections.Generic;

namespace SyncAgent.Contracts
{
    public sealed class CustomerRow
    {
        public int customerId;
        public string accountNumber;
        public string firstName;
        public string lastName;
        public string emailAddress;
        public string phone;
        public string addressLine1;
        public string city;
        public string stateProvince;
        public string postalCode;
        public string countryRegion;
    }

    public sealed class ProductRow
    {
        public int productId;
        public string name;
        public string productNumber;
        public string color;
        public decimal standardCost;
        public decimal listPrice;
        public string category;
        public string subcategory;
        public DateTime? modifiedDate;
    }

    public sealed class OrderDetailRow
    {
        public string productName;
        public string productNumber;
        public decimal unitPrice;
        public short quantity;
        public decimal lineTotal;
    }

    public sealed class OrderRow
    {
        public int salesOrderId;
        public DateTime? orderDate;
        public byte status;
        public string customerName;
        public string accountNumber;
        public decimal totalDue;
        public List<OrderDetailRow> orderDetails;
    }

    public sealed class InventoryRow
    {
        public int productId;
        public string productName;
        public string productNumber;
        public string locationName;
        public string shelf;
        public short bin;
        public short quantity;
        public DateTime? modifiedDate;
    }
}
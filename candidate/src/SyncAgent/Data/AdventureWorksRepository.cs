using Dapper;
using Microsoft.Data.SqlClient;
using SyncAgent.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Data;

public sealed class AdventureWorksRepository : IAdventureWorksRepository
{
	private readonly string _connectionString;

	public AdventureWorksRepository(IConfiguration configuration)
	{
		_connectionString =
			configuration.GetConnectionString("AdventureWorks")
			?? throw new InvalidOperationException(
				"AdventureWorks connection string is missing.");
	}

	private SqlConnection CreateConnection()
		=> new(_connectionString);

	public async Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(
		DateTime modifiedSince,
		CancellationToken cancellationToken)
	{
		const string sql = """
            SELECT
                c.CustomerID AS CustomerId,
                c.AccountNumber,
                p.FirstName,
                p.LastName,
                email.EmailAddress,
                phone.PhoneNumber AS Phone,
                addressInfo.AddressLine1,
                addressInfo.City,
                addressInfo.StateProvince,
                addressInfo.PostalCode,
                addressInfo.CountryRegion
            FROM Sales.Customer AS c
            INNER JOIN Person.Person AS p
                ON p.BusinessEntityID = c.PersonID

            OUTER APPLY
            (
                SELECT TOP (1)
                    e.EmailAddress,
                    e.ModifiedDate
                FROM Person.EmailAddress AS e
                WHERE e.BusinessEntityID = p.BusinessEntityID
                ORDER BY e.EmailAddressID
            ) AS email

            OUTER APPLY
            (
                SELECT TOP (1)
                    pp.PhoneNumber,
                    pp.ModifiedDate
                FROM Person.PersonPhone AS pp
                WHERE pp.BusinessEntityID = p.BusinessEntityID
                ORDER BY pp.PhoneNumberTypeID
            ) AS phone

            OUTER APPLY
            (
                SELECT TOP (1)
                    a.AddressLine1,
                    a.City,
                    sp.Name AS StateProvince,
                    a.PostalCode,
                    cr.Name AS CountryRegion,
                    bea.ModifiedDate AS LinkModifiedDate,
                    a.ModifiedDate AS AddressModifiedDate
                FROM Person.BusinessEntityAddress AS bea
                INNER JOIN Person.Address AS a
                    ON a.AddressID = bea.AddressID
                INNER JOIN Person.StateProvince AS sp
                    ON sp.StateProvinceID = a.StateProvinceID
                INNER JOIN Person.CountryRegion AS cr
                    ON cr.CountryRegionCode = sp.CountryRegionCode
                WHERE bea.BusinessEntityID = p.BusinessEntityID
                ORDER BY bea.AddressID
            ) AS addressInfo

            WHERE c.PersonID IS NOT NULL
              AND
              (
                    c.ModifiedDate >= @ModifiedSince
                 OR p.ModifiedDate >= @ModifiedSince
                 OR email.ModifiedDate >= @ModifiedSince
                 OR phone.ModifiedDate >= @ModifiedSince
                 OR addressInfo.LinkModifiedDate >= @ModifiedSince
                 OR addressInfo.AddressModifiedDate >= @ModifiedSince
              )

            ORDER BY c.CustomerID;
            """;

		await using var connection = CreateConnection();

		var command = new CommandDefinition(
			sql,
			new { ModifiedSince = modifiedSince },
			cancellationToken: cancellationToken,
			commandTimeout: 30);

		var rows = await connection.QueryAsync<CustomerDto>(command);

		return rows.AsList();
	}

	public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
		DateTime modifiedSince,
		CancellationToken cancellationToken)
	{
		const string sql = """
            SELECT
                p.ProductID AS ProductId,
                p.Name,
                p.ProductNumber,
                p.Color,
                p.StandardCost,
                p.ListPrice,
                pc.Name AS Category,
                ps.Name AS Subcategory,
                p.ModifiedDate
            FROM Production.Product AS p
            LEFT JOIN Production.ProductSubcategory AS ps
                ON ps.ProductSubcategoryID = p.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory AS pc
                ON pc.ProductCategoryID = ps.ProductCategoryID
            WHERE p.ModifiedDate >= @ModifiedSince
            ORDER BY p.ProductID;
            """;

		await using var connection = CreateConnection();

		var command = new CommandDefinition(
			sql,
			new { ModifiedSince = modifiedSince },
			cancellationToken: cancellationToken,
			commandTimeout: 30);

		var rows = await connection.QueryAsync<ProductDto>(command);

		return rows
			.Select(product =>
			{
				return new ProductDto
				{
					ProductId = product.ProductId,
					Name = product.Name,
					ProductNumber = product.ProductNumber,
					Color = product.Color,
					StandardCost = product.StandardCost,
					ListPrice = product.ListPrice,
					Category = product.Category,
					Subcategory = product.Subcategory,

					ModifiedDate = DateTime.SpecifyKind(
						product.ModifiedDate,
						DateTimeKind.Utc)
				};
			})
			.ToList();
	}

	public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(
		DateTime modifiedSince,
		CancellationToken cancellationToken)
	{
		const string ordersSql = """
            SELECT
                soh.SalesOrderID AS SalesOrderId,
                soh.OrderDate,
                soh.Status,

                CASE
                    WHEN customer.PersonID IS NOT NULL
                        THEN CONCAT(person.FirstName, ' ', person.LastName)
                    WHEN customer.StoreID IS NOT NULL
                        THEN store.Name
                    ELSE 'Unknown'
                END AS CustomerName,

                customer.AccountNumber,
                soh.TotalDue

            FROM Sales.SalesOrderHeader AS soh

            INNER JOIN Sales.Customer AS customer
                ON customer.CustomerID = soh.CustomerID

            LEFT JOIN Person.Person AS person
                ON person.BusinessEntityID = customer.PersonID

            LEFT JOIN Sales.Store AS store
                ON store.BusinessEntityID = customer.StoreID

            WHERE
                soh.ModifiedDate >= @ModifiedSince

                OR EXISTS
                (
                    SELECT 1
                    FROM Sales.SalesOrderDetail AS changedDetail
                    WHERE changedDetail.SalesOrderID = soh.SalesOrderID
                      AND changedDetail.ModifiedDate >= @ModifiedSince
                )

            ORDER BY soh.SalesOrderID;
            """;

		await using var connection = CreateConnection();

		var orderCommand = new CommandDefinition(
			ordersSql,
			new { ModifiedSince = modifiedSince },
			cancellationToken: cancellationToken,
			commandTimeout: 30);

		var orders = (await connection.QueryAsync<OrderDto>(
			orderCommand)).AsList();

		if (orders.Count == 0)
		{
			return orders;
		}

		const string detailsSql = """
            SELECT
                sod.SalesOrderID,
                product.Name AS ProductName,
                product.ProductNumber,
                sod.UnitPrice,
                sod.OrderQty AS Quantity,
                sod.LineTotal
            FROM Sales.SalesOrderDetail AS sod

            INNER JOIN Sales.SalesOrderHeader AS soh
                ON soh.SalesOrderID = sod.SalesOrderID

            INNER JOIN Production.Product AS product
                ON product.ProductID = sod.ProductID

            WHERE
                soh.ModifiedDate >= @ModifiedSince

                OR EXISTS
                (
                    SELECT 1
                    FROM Sales.SalesOrderDetail AS changedDetail
                    WHERE changedDetail.SalesOrderID = soh.SalesOrderID
                      AND changedDetail.ModifiedDate >= @ModifiedSince
                )

            ORDER BY
                sod.SalesOrderID,
                sod.SalesOrderDetailID;
            """;

		var detailCommand = new CommandDefinition(
			detailsSql,
			new
			{
				ModifiedSince = modifiedSince
			},
			cancellationToken: cancellationToken,
			commandTimeout: 30);

		var details = await connection.QueryAsync<OrderDetailRow>(
			detailCommand);

		var detailsByOrder = details
			.GroupBy(detail => detail.SalesOrderId)
			.ToDictionary(
				group => group.Key,
				group => group.Select(detail =>
					new OrderDetailDto
					{
						ProductName = detail.ProductName,
						ProductNumber = detail.ProductNumber,
						UnitPrice = detail.UnitPrice,
						Quantity = detail.Quantity,
						LineTotal = detail.LineTotal
					}).ToList());

		foreach (var order in orders)
		{
			order.OrderDate = DateTime.SpecifyKind(
				order.OrderDate,
				DateTimeKind.Utc);

			if (detailsByOrder.TryGetValue(
				order.SalesOrderId,
				out var orderDetails))
			{
				order.OrderDetails = orderDetails;
			}
		}

		return orders;
	}

	public async Task<IReadOnlyList<ProductInventoryDto>>
		GetProductInventoryAsync(
			DateTime modifiedSince,
			CancellationToken cancellationToken)
	{
		const string sql = """
            SELECT
                inventory.ProductID AS ProductId,
                product.Name AS ProductName,
                product.ProductNumber,
                location.Name AS LocationName,
                inventory.Shelf,
                inventory.Bin,
                inventory.Quantity,
                inventory.ModifiedDate
            FROM Production.ProductInventory AS inventory
            INNER JOIN Production.Product AS product
                ON product.ProductID = inventory.ProductID
            INNER JOIN Production.Location AS location
                ON location.LocationID = inventory.LocationID
            WHERE inventory.ModifiedDate >= @ModifiedSince
            ORDER BY
                inventory.ProductID,
                inventory.LocationID;
            """;

		await using var connection = CreateConnection();

		var command = new CommandDefinition(
			sql,
			new { ModifiedSince = modifiedSince },
			cancellationToken: cancellationToken,
			commandTimeout: 30);

		var rows = await connection.QueryAsync<ProductInventoryDto>(
			command);

		return rows
			.Select(inventory =>
			{
				return new ProductInventoryDto
				{
					ProductId = inventory.ProductId,
					ProductName = inventory.ProductName,
					ProductNumber = inventory.ProductNumber,
					LocationName = inventory.LocationName,
					Shelf = inventory.Shelf,
					Bin = inventory.Bin,
					Quantity = inventory.Quantity,

					ModifiedDate = DateTime.SpecifyKind(
						inventory.ModifiedDate,
						DateTimeKind.Utc)
				};
			})
			.ToList();
	}

	private sealed class OrderDetailRow
	{
		public int SalesOrderId { get; init; }

		public string ProductName { get; init; } = string.Empty;

		public string ProductNumber { get; init; } = string.Empty;

		public decimal UnitPrice { get; init; }

		public short Quantity { get; init; }

		public decimal LineTotal { get; init; }
	}
}
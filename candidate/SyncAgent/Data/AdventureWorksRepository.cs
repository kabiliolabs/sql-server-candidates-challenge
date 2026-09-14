using Microsoft.EntityFrameworkCore;
using SyncAgent.Contracts;

namespace SyncAgent.Data;

public class AdventureWorksRepository(AdventureWorksDbContext context, ILogger<AdventureWorksRepository> logger) : IAdventureWorksRepository
{
    public async Task<List<CustomerDto>> GetCustomersAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var customers = await context.Customers
            .AsNoTracking()
            .Where(c => c.ModifiedDate >= modifiedSince && c.PersonId != null)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                AccountNumber = c.AccountNumber,
                FirstName = c.Person!.FirstName,
                LastName = c.Person.LastName,
                EmailAddress = c.Person.EmailAddresses
                    .OrderBy(em => em.EmailAddressId)
                    .Select(em => em.EmailAddress1)
                    .FirstOrDefault(),
                Phone = c.Person.PersonPhones
                    .OrderBy(pp => pp.PhoneNumber)
                    .Select(pp => pp.PhoneNumber)
                    .FirstOrDefault(),
                AddressLine1 = c.Person.BusinessEntityAddresses
                    .Select(bea => bea.Address.AddressLine1)
                    .FirstOrDefault(),
                City = c.Person.BusinessEntityAddresses
                    .Select(bea => bea.Address.City)
                    .FirstOrDefault(),
                StateProvince = c.Person.BusinessEntityAddresses
                    .Select(bea => bea.Address.StateProvince.Name)
                    .FirstOrDefault(),
                PostalCode = c.Person.BusinessEntityAddresses
                    .Select(bea => bea.Address.PostalCode)
                    .FirstOrDefault(),
                CountryRegion = c.Person.BusinessEntityAddresses
                    .Select(bea => bea.Address.StateProvince.CountryRegion.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        logger.LogDebug("GetCustomersAsync returned {Count} customers modified since {ModifiedSince}.", customers.Count, modifiedSince);
        return customers;
    }

    public async Task<List<ProductDto>> GetProductsAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var products = await context.Products
            .AsNoTracking()
            .Where(p => p.ModifiedDate >= modifiedSince)
            .Select(p => new ProductDto
            {
                ProductId = p.ProductId,
                Name = p.Name,
                ProductNumber = p.ProductNumber,
                Color = p.Color,
                StandardCost = p.StandardCost,
                ListPrice = p.ListPrice,
                Category = p.ProductSubcategory != null ? p.ProductSubcategory.ProductCategory.Name : null,
                Subcategory = p.ProductSubcategory != null ? p.ProductSubcategory.Name : null,
                ModifiedDate = p.ModifiedDate
            })
            .ToListAsync(cancellationToken);

        logger.LogDebug("GetProductsAsync returned {Count} products modified since {ModifiedSince}.", products.Count, modifiedSince);
        return products;
    }

    public async Task<List<ProductInventoryDto>> GetProductInventoryAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var inventory = await context.ProductInventories
            .AsNoTracking()
            .Where(pi => pi.ModifiedDate >= modifiedSince)
            .Select(pi => new ProductInventoryDto
            {
                ProductId = pi.ProductId,
                ProductName = pi.Product.Name,
                ProductNumber = pi.Product.ProductNumber,
                LocationName = pi.Location.Name,
                Shelf = pi.Shelf,
                Bin = pi.Bin,
                Quantity = pi.Quantity,
                ModifiedDate = pi.ModifiedDate
            })
            .ToListAsync(cancellationToken);

        logger.LogDebug("GetProductInventoryAsync returned {Count} inventory records modified since {ModifiedSince}.", inventory.Count, modifiedSince);
        return inventory;
    }

    public async Task<List<OrderDto>> GetOrdersAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var orders = await context.SalesOrderHeaders
            .AsNoTracking()
            .AsSplitQuery()
            .Where(o => o.ModifiedDate >= modifiedSince)
            .Include(o => o.SalesOrderDetails)
                .ThenInclude(d => d.Product)
            .Include(o => o.Customer)
                .ThenInclude(c => c.Person)
            .ToListAsync(cancellationToken);

        var result = orders.Select(o => new OrderDto
        {
            SalesOrderId = o.SalesOrderId,
            OrderDate = o.OrderDate,
            Status = o.Status,
            CustomerName = o.Customer.Person != null
                ? $"{o.Customer.Person.FirstName} {o.Customer.Person.LastName}"
                : "Unknown",
            AccountNumber = o.Customer.AccountNumber,
            TotalDue = o.TotalDue,
            OrderDetails = o.SalesOrderDetails.Select(d => new OrderDetailDto
            {
                ProductName = d.Product.Name,
                ProductNumber = d.Product.ProductNumber,
                UnitPrice = d.UnitPrice,
                Quantity = d.OrderQty,
                LineTotal = d.LineTotal
            }).ToList()
        }).ToList();

        logger.LogDebug("GetOrdersAsync returned {Count} orders modified since {ModifiedSince}.", result.Count, modifiedSince);
        return result;
    }
}

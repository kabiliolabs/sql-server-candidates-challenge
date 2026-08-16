using AdventureWorks.Ef.Data;
using Microsoft.EntityFrameworkCore;
using SyncAgent.Models;

namespace SyncAgent.Repository;

public sealed class AdventureWorksRepository : IAdventureWorksRepository
{
    private readonly AdventureWorksDbContext _dbContext;

    public AdventureWorksRepository(IConfiguration configuration, AdventureWorksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var query = _dbContext.Customer.AsNoTracking()
            .Include(x => x.Person)
            .Where(x => x.ModifiedDate > modifiedSince)
            .Select(x => new CustomerDto
            {
                CustomerId = x.CustomerId,
                AccountNumber = x.AccountNumber,
                FirstName = x.Person != null ? x.Person.FirstName : null,
                LastName = x.Person != null ? x.Person.LastName : null,
                EmailAddress = x.Person != null && x.Person.EmailAddress.Any() ? x.Person.EmailAddress.First().EmailAddress1 : null,
                Phone = x.Person != null && x.Person.PersonPhone.Any() ? x.Person.PersonPhone.First().PhoneNumber : null,
                AddressLine1 = x.Person != null && x.Person.BusinessEntity.BusinessEntityAddress.Any()
                    ? x.Person.BusinessEntity.BusinessEntityAddress.First().Address.AddressLine1
                    : null,
                City = x.Person != null && x.Person.BusinessEntity.BusinessEntityAddress.Any()
                    ? x.Person.BusinessEntity.BusinessEntityAddress.First().Address.City
                    : null,
                StateProvince = x.Person != null && x.Person.BusinessEntity.BusinessEntityAddress.Any()
                    ? x.Person.BusinessEntity.BusinessEntityAddress.First().Address.StateProvince.Name
                    : null,
                PostalCode = x.Person != null && x.Person.BusinessEntity.BusinessEntityAddress.Any()
                    ? x.Person.BusinessEntity.BusinessEntityAddress.First().Address.PostalCode
                    : null,
                CountryRegion = x.Person != null && x.Person.BusinessEntity.BusinessEntityAddress.Any()
                    ? x.Person.BusinessEntity.BusinessEntityAddress.First().Address.StateProvince.CountryRegionCodeNavigation.Name
                    : null,
            });

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var query = _dbContext.Product.AsNoTracking()
            .Include(x => x.ProductSubcategory)
            .ThenInclude(x => x.ProductCategory)
            .Where(x => x.ModifiedDate >= modifiedSince)
            .Select(x => new ProductDto
            {
                ProductId = x.ProductId,
                Name = x.Name,
                ProductNumber = x.ProductNumber,
                Color = x.Color,
                StandardCost = x.StandardCost,
                ListPrice = x.ListPrice,
                Category = x.ProductSubcategory != null && x.ProductSubcategory.ProductCategory != null
                    ? x.ProductSubcategory.ProductCategory.Name
                    : null,
                Subcategory = x.ProductSubcategory != null ? x.ProductSubcategory.Name : null,
                ModifiedDate = x.ModifiedDate
            });

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.SalesOrderDetail.AsNoTracking()
            .Join(_dbContext.Product.AsNoTracking(),
                detail => detail.ProductId,
                product => product.ProductId,
                (detail, product) => new { detail, product })
            .Where(x => x.detail.SalesOrder.ModifiedDate >= modifiedSince)
            .Select(x => new
            {
                x.detail.SalesOrderId,
                x.detail.SalesOrderDetailId,
                x.detail.UnitPrice,
                x.detail.OrderQty,
                x.detail.LineTotal,
                x.product.Name,
                x.product.ProductNumber,
                OrderDate = x.detail.SalesOrder.OrderDate,
                Status = x.detail.SalesOrder.Status,
                TotalDue = x.detail.SalesOrder.TotalDue,
                AccountNumber = x.detail.SalesOrder.Customer.AccountNumber,
                CustomerName = x.detail.SalesOrder.Customer.Person != null
                    ? x.detail.SalesOrder.Customer.Person.FirstName + " " + x.detail.SalesOrder.Customer.Person.LastName
                    : x.detail.SalesOrder.Customer.Store != null
                        ? x.detail.SalesOrder.Customer.Store.Name
                        : "Unknown"
            })
            .OrderBy(x => x.SalesOrderId)
            .ToListAsync(cancellationToken);

        var orders = new List<OrderDto>();

        foreach (var group in rows.GroupBy(x => x.SalesOrderId))
        {
            var header = group.First();

            orders.Add(new OrderDto
            {
                SalesOrderId = group.Key,
                OrderDate = header.OrderDate,
                Status = header.Status,
                CustomerName = header.CustomerName,
                AccountNumber = header.AccountNumber,
                TotalDue = header.TotalDue,
                OrderDetails = group.Select(x => new OrderDetailDto
                {
                    ProductName = x.Name,
                    ProductNumber = x.ProductNumber,
                    UnitPrice = x.UnitPrice,
                    Quantity = x.OrderQty,
                    LineTotal = x.LineTotal
                }).ToList()
            });
        }

        return orders;
    }

    public async Task<IReadOnlyList<ProductInventoryDto>> GetProductInventoryAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var query = _dbContext.ProductInventory.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Location)
            .Where(x => x.ModifiedDate >= modifiedSince)
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.LocationId)
            .Select(x => new ProductInventoryDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                ProductNumber = x.Product.ProductNumber,
                LocationName = x.Location.Name,
                Shelf = x.Shelf,
                Bin = x.Bin,
                Quantity = x.Quantity,
                ModifiedDate = DateTime.SpecifyKind(x.ModifiedDate, DateTimeKind.Utc)
            });

        return await query.ToListAsync(cancellationToken);
    }
}
using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Data;
using SyncAgent.Data.Entities;

namespace SyncAgent.Tests.Data;

public class AdventureWorksRepositoryTests
{
    private static readonly DateTime ModifiedSince = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetCustomersAsync_Filters_By_ModifiedSince_And_Excludes_Store_Only_Customers()
    {
        using var context = InMemoryDbContextFactory.Create();

        var country = new CountryRegion { CountryRegionCode = "US", Name = "United States" };
        var state = new StateProvince { StateProvinceId = 1, Name = "California", CountryRegionCode = "US", CountryRegion = country };
        var address = new Address { AddressId = 1, AddressLine1 = "123 Main St", City = "Los Angeles", StateProvinceId = 1, PostalCode = "90001", StateProvince = state };
        var person = new Person
        {
            BusinessEntityId = 1,
            FirstName = "Jane",
            LastName = "Doe",
            EmailAddresses = new List<EmailAddress> { new() { BusinessEntityId = 1, EmailAddressId = 1, EmailAddress1 = "jane@example.com" } },
            PersonPhones = new List<PersonPhone> { new() { BusinessEntityId = 1, PhoneNumber = "555-1234" } },
            BusinessEntityAddresses = new List<BusinessEntityAddress> { new() { BusinessEntityId = 1, AddressId = 1, Address = address } }
        };

        var recentCustomer = new Customer
        {
            CustomerId = 1,
            AccountNumber = "AW00000001",
            PersonId = 1,
            Person = person,
            ModifiedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var staleCustomer = new Customer
        {
            CustomerId = 2,
            AccountNumber = "AW00000002",
            PersonId = 1,
            Person = person,
            ModifiedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var storeOnlyCustomer = new Customer
        {
            CustomerId = 3,
            AccountNumber = "AW00000003",
            PersonId = null,
            ModifiedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        context.Customers.AddRange(recentCustomer, staleCustomer, storeOnlyCustomer);
        await context.SaveChangesAsync();

        var sut = new AdventureWorksRepository(context, NullLogger<AdventureWorksRepository>.Instance);
        var result = await sut.GetCustomersAsync(ModifiedSince, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(1, dto.CustomerId);
        Assert.Equal("Jane", dto.FirstName);
        Assert.Equal("Doe", dto.LastName);
        Assert.Equal("jane@example.com", dto.EmailAddress);
        Assert.Equal("555-1234", dto.Phone);
        Assert.Equal("123 Main St", dto.AddressLine1);
        Assert.Equal("Los Angeles", dto.City);
        Assert.Equal("California", dto.StateProvince);
        Assert.Equal("United States", dto.CountryRegion);
    }

    [Fact]
    public async Task GetProductsAsync_Maps_Category_And_Subcategory()
    {
        using var context = InMemoryDbContextFactory.Create();

        var category = new ProductCategory { ProductCategoryId = 1, Name = "Bikes" };
        var subcategory = new ProductSubcategory { ProductSubcategoryId = 1, Name = "Mountain Bikes", ProductCategoryId = 1, ProductCategory = category };
        var product = new Product
        {
            ProductId = 1,
            Name = "Mountain-100",
            ProductNumber = "BK-M82S-38",
            Color = "Silver",
            StandardCost = 1000m,
            ListPrice = 1500m,
            ProductSubcategoryId = 1,
            ProductSubcategory = subcategory,
            ModifiedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var sut = new AdventureWorksRepository(context, NullLogger<AdventureWorksRepository>.Instance);
        var result = await sut.GetProductsAsync(ModifiedSince, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("Mountain-100", dto.Name);
        Assert.Equal("Bikes", dto.Category);
        Assert.Equal("Mountain Bikes", dto.Subcategory);
    }

    [Fact]
    public async Task GetProductInventoryAsync_Maps_Product_And_Location()
    {
        using var context = InMemoryDbContextFactory.Create();

        var product = new Product { ProductId = 1, Name = "Mountain-100", ProductNumber = "BK-M82S-38", ModifiedDate = ModifiedSince };
        var location = new Location { LocationId = 1, Name = "Warehouse A" };
        var inventory = new ProductInventory
        {
            ProductId = 1,
            LocationId = 1,
            Product = product,
            Location = location,
            Shelf = "A1",
            Bin = 5,
            Quantity = 100,
            ModifiedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        context.ProductInventories.Add(inventory);
        await context.SaveChangesAsync();

        var sut = new AdventureWorksRepository(context, NullLogger<AdventureWorksRepository>.Instance);
        var result = await sut.GetProductInventoryAsync(ModifiedSince, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("Mountain-100", dto.ProductName);
        Assert.Equal("Warehouse A", dto.LocationName);
        Assert.Equal(100, dto.Quantity);
    }

    [Fact]
    public async Task GetOrdersAsync_Groups_Multiple_Line_Items_Under_One_Order()
    {
        using var context = InMemoryDbContextFactory.Create();

        var person = new Person { BusinessEntityId = 1, FirstName = "John", LastName = "Smith" };
        var customer = new Customer { CustomerId = 1, AccountNumber = "AW00000001", PersonId = 1, Person = person, ModifiedDate = ModifiedSince };
        var productA = new Product { ProductId = 1, Name = "Road-150", ProductNumber = "BK-R93R-62", ModifiedDate = ModifiedSince };
        var productB = new Product { ProductId = 2, Name = "Sport-100", ProductNumber = "HL-U509-R", ModifiedDate = ModifiedSince };

        var order = new SalesOrderHeader
        {
            SalesOrderId = 1,
            OrderDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = 5,
            CustomerId = 1,
            Customer = customer,
            TotalDue = 500m,
            ModifiedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SalesOrderDetails = new List<SalesOrderDetail>
            {
                new() { SalesOrderId = 1, SalesOrderDetailId = 1, ProductId = 1, Product = productA, OrderQty = 1, UnitPrice = 300m, LineTotal = 300m },
                new() { SalesOrderId = 1, SalesOrderDetailId = 2, ProductId = 2, Product = productB, OrderQty = 2, UnitPrice = 100m, LineTotal = 200m }
            }
        };

        context.SalesOrderHeaders.Add(order);
        await context.SaveChangesAsync();

        var sut = new AdventureWorksRepository(context, NullLogger<AdventureWorksRepository>.Instance);
        var result = await sut.GetOrdersAsync(ModifiedSince, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("John Smith", dto.CustomerName);
        Assert.Equal((byte)5, dto.Status);
        Assert.Equal(2, dto.OrderDetails.Count);
        Assert.Contains(dto.OrderDetails, d => d.ProductName == "Road-150" && d.Quantity == 1);
        Assert.Contains(dto.OrderDetails, d => d.ProductName == "Sport-100" && d.Quantity == 2);
    }

    [Fact]
    public async Task GetOrdersAsync_Falls_Back_To_Unknown_For_Store_Only_Customer()
    {
        using var context = InMemoryDbContextFactory.Create();

        var customer = new Customer { CustomerId = 2, AccountNumber = "AW00000002", PersonId = null, ModifiedDate = ModifiedSince };
        var product = new Product { ProductId = 1, Name = "Road-150", ProductNumber = "BK-R93R-62", ModifiedDate = ModifiedSince };

        var order = new SalesOrderHeader
        {
            SalesOrderId = 2,
            OrderDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = 5,
            CustomerId = 2,
            Customer = customer,
            TotalDue = 300m,
            ModifiedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SalesOrderDetails = new List<SalesOrderDetail>
            {
                new() { SalesOrderId = 2, SalesOrderDetailId = 1, ProductId = 1, Product = product, OrderQty = 1, UnitPrice = 300m, LineTotal = 300m }
            }
        };

        context.SalesOrderHeaders.Add(order);
        await context.SaveChangesAsync();

        var sut = new AdventureWorksRepository(context, NullLogger<AdventureWorksRepository>.Instance);
        var result = await sut.GetOrdersAsync(ModifiedSince, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("Unknown", dto.CustomerName);
    }
}

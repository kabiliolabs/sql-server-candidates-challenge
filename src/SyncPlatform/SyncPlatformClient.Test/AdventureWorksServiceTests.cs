using Moq;
using SyncPlatformClient.Domain.Contracts.Customers;
using SyncPlatformClient.Domain.Contracts.Orders;
using SyncPlatformClient.Domain.Contracts.Products;
using SyncPlatformClient.Domain.DTOs;
using SyncPlatformClient.Domain.Services.Customer;
using SyncPlatformClient.Domain.Services.Order;
using SyncPlatformClient.Domain.Services.Product;
using SyncPlatformClient.Repository.DataModels;
using SyncPlatformClient.Repository.Repository.Interfaces;

namespace SyncPlatformClient.Test
{
    public class AdventureWorksServiceTests
    {
        // repository moqs
        private readonly Mock<ICustomerRepository> _mockCustomerRepository;
        private readonly Mock<IProductInventoryRepository> _mockProductInventoryRepository;
        private readonly Mock<IProductRepository> _mockProductRepository;
        private readonly Mock<ISalesOrderRepository> _mockSalesOrderRepository;

        // services
        private readonly ICustomerService _customerService;
        private readonly IProductInventoryService _productInventoryService;
        private readonly IProductService _productService;
        private readonly IOrderService _orderService;

        public AdventureWorksServiceTests()
        {
            _mockCustomerRepository = new Mock<ICustomerRepository>();
            _mockProductInventoryRepository = new Mock<IProductInventoryRepository>();
            _mockProductRepository = new Mock<IProductRepository>();
            _mockSalesOrderRepository = new Mock<ISalesOrderRepository>();

            _customerService = new CustomerService(_mockCustomerRepository.Object);
            _productInventoryService = new ProductInventoryService(_mockProductInventoryRepository.Object);
            _productService = new ProductService(_mockProductRepository.Object);
            _orderService = new OrderService(_mockSalesOrderRepository.Object);
        }

        [Fact]
        public async Task GetCustomerList_Returns_Task_Completed_With_Data()
        {
            // arrange
            var customers = new List<Customer>
            {
                new() {
                    CustomerId = 1, 
                    AccountNumber = "account number test", 
                    FirstName = "Aitor", 
                    LastName = "Arque",
                    EmailAddress = "aitor@example.com",
                    Phone = "7538475375",
                    AddressLine1 = "C/Test", 
                    City = "Barcelona",
                    StateProvince = "ES", 
                    PostalCode = "08347", 
                    CountryRegion = "ES" 
                }
            };
            _mockCustomerRepository.Setup(r => r.GetCustomerListAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(customers);

            var syncTask = new SyncTask
            {
                TaskId = "smdfwinewfrth843yrt8htf",
                TaskType = "GetCustomers",
                Parameters = new Dictionary<string, string>
                {
                    ["modifiedSince"] = "2023-06-12"
                }
            };

            // act
            var result = await _customerService.GetCustomerListAsync(syncTask);

            // assert
            Assert.Equal("completed", result.Status);
            Assert.Equal(1, result.RecordCount);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task GetProductInventoryList_Returns_Task_Completed_With_Data()
        {
            // arrange
            var inventory = new List<ProductInventory>()
            {
               new() 
               { 
                   ProductId = 10, 
                   ProductName = "test name", 
                   ProductNumber = "F-123456",
                   LocationName = "ES", Shelf = "J", 
                   Bin = 10, 
                   Quantity = 2 
               }
            };
            _mockProductInventoryRepository.Setup(r => r.GetProductInventoryListAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(inventory);

            var syncTask = new SyncTask
            {
                TaskId = "75834rfuiwef",
                TaskType = "GetProductInventory",
                Parameters = new Dictionary<string, string> 
                { 
                    ["modifiedSince"] = "2023-06-12" 
                }
            };

            // act
            var result = await _productInventoryService.GetProductInventoryListAsync(syncTask);

            // assert
            Assert.Equal("completed", result.Status);
            Assert.Equal(1, result.RecordCount);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task GetOrderList_Returns_Task_Completed_With_Data()
        {
            // arrange
            var orderList = new List<Order>
            {
                new() 
                { 
                    SalesOrderId = 10, 
                    OrderDate = DateTime.UtcNow, 
                    Status = 5,
                    CustomerName = "Aitor", 
                    AccountNumber = "test number", 
                    TotalDue = 100M,
                    ProductName = "Brownie",
                    ProductNumber = "BW-123456", 
                    UnitPrice = 100M, 
                    Quantity = 10, 
                    LineTotal = 500M 
                }
            };

            _mockSalesOrderRepository.Setup(r => r.GetOrderListAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(orderList);

            var syncTask = new SyncTask 
            { 
                TaskId = "dfhuweht743w", 
                TaskType = "GetOrders",
                Parameters = new Dictionary<string, string> 
                { 
                    ["modifiedSince"] = "2023-06-12" 
                }
            };

            // act
            var result = await _orderService.GetOrderListAsync(syncTask);

            // assert
            Assert.Equal("completed", result.Status);
            Assert.Equal(1, result.RecordCount);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task GetProductList_Returns_Task_Completed_With_Data()
        {
            // arrange
            var products = new List<Product>
            {
                new() 
                { 
                    ProductId = 123456, 
                    Name = "Test product", 
                    ProductNumber = "p-numner-test",
                    Color = "Black", 
                    StandardCost = 1M, 
                    ListPrice = 1M,
                    Category = "food", 
                    Subcategory = "sub-category test" 
                }
            };

            _mockProductRepository.Setup(r => r.GetProductListAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(products);

            var syncTask = new SyncTask
            {
                TaskId = "kfdwh347yr73fer",
                TaskType = "GetProducts",
                Parameters = new Dictionary<string, string> 
                {
                    ["modifiedSince"] = "2023-06-12"
                }
            };

            // act
            var result = await _productService.GetProductListAsync(syncTask);

            // assert
            Assert.Equal("completed", result.Status);
            Assert.Equal(1, result.RecordCount);
            Assert.NotNull(result.Data);
        }

    }
}
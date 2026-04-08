using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SyncPlatformClient.Repository.Repository.Interfaces;
using SyncPlatformClient.Repository.Repository.Services;
using SyncPlatformClient.Domain.Contracts.Customers;
using SyncPlatformClient.Domain.Services.Customer;
using SyncPlatformClient.Domain.Contracts.Products;
using SyncPlatformClient.Domain.Services.Product;
using SyncPlatformClient.Domain.Contracts.Orders;
using SyncPlatformClient.Domain.Services.Order;
using SyncPlatformClient.Domain.Services.Polling;

class Program
{
    static async Task Main(string[] args)
    {
        string url = "http://localhost:5100/api/sync/next-task";

        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Set up DI container
        ServiceCollection services = new();

        // Register your main application class
        services.AddScoped<ICustomerRepository, CustomerRespository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
        services.AddScoped<IProductInventoryRepository, ProductInventoryRepository>();

        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductInventoryService, ProductInventoryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<PollingService>();

        // Add IConfiguration to DI
        services.AddSingleton<IConfiguration>(configuration);

        // Build the provider
        var provider = services.BuildServiceProvider();

        // Run the polling
        var polling = provider.GetRequiredService<PollingService>();
        await polling.StartPollingAsync(url);
    }
}
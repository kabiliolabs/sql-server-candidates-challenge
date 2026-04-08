using SyncPlatformClient.Domain.Contracts.Customers;
using SyncPlatformClient.Domain.Contracts.Orders;
using SyncPlatformClient.Domain.Contracts.Products;
using SyncPlatformClient.Domain.DTOs;
using System.Net.Http;
using System.Net.Http.Json;

namespace SyncPlatformClient.Domain.Services.Polling
{

    public class PollingService
    {
        private const string ApiKey = "candidate-test-key-2026";
        private readonly HttpClient _client;

        // services
        private readonly ICustomerService _customerService;
        private readonly IProductService _productService;
        private readonly IProductInventoryService _productInventoryService;
        private IOrderService _orderService;

        public PollingService(ICustomerService customerService,
            IProductService productService,
            IProductInventoryService productInventoryService,
            IOrderService orderService)
        {
            _client = new HttpClient();
            _customerService = customerService;
            _productService = productService;
            _productInventoryService = productInventoryService;
            _orderService = orderService;
        }

        public async Task StartPollingAsync(string url, CancellationToken cancellationToken = default)
        {
            // add Api Key heder to http client calls
            _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadFromJsonAsync<SyncTask>();
                    SyncResult result = new SyncResult();
                    switch (content.TaskType) 
                    {
                        case "GetCustomers":
                            Console.WriteLine($"GetCustomers request in progress");
                            result = await _customerService.GetCustomerListAsync(content, cancellationToken);
                            await PostResultAsync(result, cancellationToken);
                            Console.WriteLine($"GetCustomers request completed");
                            break;
                        case "GetProducts":
                            Console.WriteLine($"GetProducts request in progress");
                            result = await _productService.GetProductListAsync(content, cancellationToken);
                            await PostResultAsync(result, cancellationToken);
                            Console.WriteLine($"GetProducts request completed");
                            break;
                        case "GetProductInventory":
                            Console.WriteLine($"GetProductInventory request in progress");
                            result = await _productInventoryService.GetProductInventoryListAsync(content, cancellationToken);
                            await PostResultAsync(result, cancellationToken);
                            Console.WriteLine($"GetProductInventory request completed");
                            break;
                        case "GetOrders":
                            Console.WriteLine($"GetOrders request in progress");
                            result = await _orderService.GetOrderListAsync(content, cancellationToken);
                            await PostResultAsync(result, cancellationToken);
                            Console.WriteLine($"GetOrders request completed");
                            break;
                        default:
                            return;
                    }
                }
                catch (TaskCanceledException ex)
                {
                   throw new TaskCanceledException($"error polling sync platform, token cancelled. error message {ex.Message}, stack trace {ex.StackTrace}");
                }
                catch (Exception ex)
                {
                    await Task.Delay(3000);
                    continue;
                }
            }
        }

        private async Task PostResultAsync(SyncResult result, CancellationToken cancellationToken = default)
        {
            var response = await _client.PostAsJsonAsync("http://localhost:5100/api/sync/result", result, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }
}
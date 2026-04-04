using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Interfaces;
using SyncAgent.Infrastructure.Handlers;

namespace SyncAgent.Tests;

/// <summary>
/// Verifies that each handler declares the correct TaskType string.
/// This is critical because the Worker routes tasks by this value —
/// a typo here would silently drop all tasks of that type.
/// </summary>
public class HandlerTaskTypeTests
{
    private static readonly IOptions<SyncAgentOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new SyncAgentOptions
        {
            ConnectionString = "Server=test;Database=test;"
        });

    [Theory]
    [InlineData(typeof(GetCustomersHandler), "GetCustomers")]
    [InlineData(typeof(GetProductsHandler), "GetProducts")]
    [InlineData(typeof(GetOrdersHandler), "GetOrders")]
    [InlineData(typeof(GetProductInventoryHandler), "GetProductInventory")]
    public void Handler_HasCorrectTaskType(Type handlerType, string expectedTaskType)
    {
        var handler = (ISyncTaskHandler)Activator.CreateInstance(handlerType, Options)!;
        Assert.Equal(expectedTaskType, handler.TaskType);
    }
}

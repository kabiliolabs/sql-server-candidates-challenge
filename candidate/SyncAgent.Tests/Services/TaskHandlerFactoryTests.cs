using Moq;
using SyncAgent.Handlers;
using SyncAgent.Services;

namespace SyncAgent.Tests.Services;

public class TaskHandlerFactoryTests
{
    [Fact]
    public void GetHandler_Returns_Correct_Handler_For_KnownType()
    {
        var handlers = new ITaskHandler[]
        {
            CreateHandler("GetCustomers"),
            CreateHandler("GetProducts"),
            CreateHandler("GetOrders"),
            CreateHandler("GetProductInventory")
        };
        var factory = new TaskHandlerFactory(handlers);

        var handler = factory.GetHandler("GetCustomers");

        Assert.Equal("GetCustomers", handler.TaskType);
    }

    [Fact]
    public void GetHandler_Is_CaseInsensitive()
    {
        var handlers = new ITaskHandler[] { CreateHandler("GetCustomers") };
        var factory = new TaskHandlerFactory(handlers);

        var handler = factory.GetHandler("getcustomers");

        Assert.Equal("GetCustomers", handler.TaskType);
    }

    [Fact]
    public void GetHandler_Throws_ForUnknownTaskType()
    {
        var factory = new TaskHandlerFactory(Enumerable.Empty<ITaskHandler>());

        Assert.Throws<NotSupportedException>(() => factory.GetHandler("UnknownType"));
    }

    [Fact]
    public void HasHandler_Returns_True_For_RegisteredType()
    {
        var handlers = new ITaskHandler[] { CreateHandler("GetOrders") };
        var factory = new TaskHandlerFactory(handlers);

        Assert.True(factory.HasHandler("GetOrders"));
    }

    [Fact]
    public void HasHandler_Returns_False_For_UnregisteredType()
    {
        var factory = new TaskHandlerFactory(Enumerable.Empty<ITaskHandler>());

        Assert.False(factory.HasHandler("GetOrders"));
    }

    private static ITaskHandler CreateHandler(string taskType)
    {
        var mock = new Mock<ITaskHandler>();
        mock.SetupGet(h => h.TaskType).Returns(taskType);
        return mock.Object;
    }
}

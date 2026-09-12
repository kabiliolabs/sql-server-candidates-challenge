using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Services;
using SyncAgent.Infrastructure.Options;
using Xunit;

namespace SyncAgent.Infrastructure.Tests;

public class ServiceRegistrationTests
{
    // ValidateScopes makes the container reject a Scoped service captured by a Singleton, and
    // ValidateOnBuild forces every registration to be constructible up front rather than on first use.
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddLogging()
            .AddInfrastructure()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

    [Fact]
    public void AddInfrastructure_ResolvesThePollerAndItsDependencies()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISyncTaskPoller>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISyncTaskDispatcher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISyncPlatformClient>());
    }

    [Fact]
    public void AddInfrastructure_RegistersAHandlerForEveryContractTaskType()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var taskTypes = scope.ServiceProvider
            .GetServices<ISyncTaskHandler>()
            .Select(handler => handler.TaskType)
            .OrderBy(type => type)
            .ToArray();

        Assert.Equal(
            ["GetCustomers", "GetOrders", "GetProductInventory", "GetProducts"],
            taskTypes);
    }

    [Fact]
    public void AddInfrastructure_TracksConnectivityAcrossScopes()
    {
        using var provider = BuildProvider();

        using (var firstCycle = provider.CreateScope())
        {
            Assert.True(firstCycle.ServiceProvider.GetRequiredService<ISyncPlatformConnectivityTracker>().RecordFailure());
        }

        using var secondCycle = provider.CreateScope();
        Assert.False(secondCycle.ServiceProvider.GetRequiredService<ISyncPlatformConnectivityTracker>().RecordFailure());
    }

    [Theory]
    [InlineData("", "BaseUrl")]
    [InlineData("not-a-url", "BaseUrl")]
    public void AddInfrastructure_WithInvalidBaseUrl_FailsValidationWithAClearMessage(string baseUrl, string expectedField)
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(syncPlatform => syncPlatform.BaseUrl = baseUrl)
            .BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SyncPlatformOptions>>().Value);

        Assert.Contains(expectedField, exception.Message);
    }

    [Fact]
    public void AddInfrastructure_WithNonPositivePollingInterval_FailsValidation()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configurePolling: polling => polling.IntervalSeconds = 0)
            .BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<PollingOptions>>().Value);

        Assert.Contains("IntervalSeconds", exception.Message);
    }
}

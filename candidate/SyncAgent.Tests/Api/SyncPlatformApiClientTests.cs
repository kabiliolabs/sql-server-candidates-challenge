using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Api;
using SyncAgent.Contracts;

namespace SyncAgent.Tests.Api;

public class SyncPlatformApiClientTests
{
    [Fact]
    public async Task GetNextTaskAsync_Returns_Task_On_200()
    {
        var task = new SyncTaskDto
        {
            TaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            TaskType = "GetCustomers",
            Parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" },
            CreatedAt = DateTime.UtcNow
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(task)
        };
        var client = FakeHttpMessageHandler.CreateClient(response);
        var sut = new SyncPlatformApiClient(client, NullLogger<SyncPlatformApiClient>.Instance);

        var result = await sut.GetNextTaskAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(task.TaskId, result!.TaskId);
        Assert.Equal(task.TaskType, result.TaskType);
    }

    [Fact]
    public async Task GetNextTaskAsync_Returns_Null_On_204()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        var client = FakeHttpMessageHandler.CreateClient(response);
        var sut = new SyncPlatformApiClient(client, NullLogger<SyncPlatformApiClient>.Instance);

        var result = await sut.GetNextTaskAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextTaskAsync_Throws_On_401()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var client = FakeHttpMessageHandler.CreateClient(response);
        var sut = new SyncPlatformApiClient(client, NullLogger<SyncPlatformApiClient>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetNextTaskAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetNextTaskAsync_Throws_On_Malformed_Json()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ not valid json", System.Text.Encoding.UTF8, "application/json")
        };
        var client = FakeHttpMessageHandler.CreateClient(response);
        var sut = new SyncPlatformApiClient(client, NullLogger<SyncPlatformApiClient>.Instance);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => sut.GetNextTaskAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PostResultAsync_Succeeds_On_200()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var client = FakeHttpMessageHandler.CreateClient(response);
        var sut = new SyncPlatformApiClient(client, NullLogger<SyncPlatformApiClient>.Instance);
        var result = new SyncResultDto
        {
            TaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            TaskType = "GetCustomers",
            Status = "completed",
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow
        };

        await sut.PostResultAsync(result, CancellationToken.None);
        // No exception thrown implies success.
    }

    [Fact]
    public async Task PostResultAsync_Throws_On_400()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"invalid payload\"}")
        };
        var client = FakeHttpMessageHandler.CreateClient(response);
        var sut = new SyncPlatformApiClient(client, NullLogger<SyncPlatformApiClient>.Instance);
        var result = new SyncResultDto
        {
            TaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            TaskType = "GetCustomers",
            Status = "completed",
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.PostResultAsync(result, CancellationToken.None));
    }
}

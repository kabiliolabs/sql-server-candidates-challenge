using System.Net;
using System.Net.Http.Json;
using System.Text;
using SyncAgent.Worker.Platform;

namespace SyncAgent.Worker.Tests.Platform;

public class SyncPlatformClientTests
{
    private static SyncPlatformClient CreateClient(HttpStatusCode statusCode, HttpContent? content = null)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode) { Content = content });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5100/") };
        return new SyncPlatformClient(httpClient);
    }

    private static SyncTask MakeTask() => new()
    {
        TaskId = "task-1",
        TaskType = SyncTaskType.GetProducts,
        Parameters = null,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task GetNextTaskAsync_ReturnsNull_On204()
    {
        var client = CreateClient(HttpStatusCode.NoContent);

        var result = await client.GetNextTaskAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextTaskAsync_DeserializesTask_On200()
    {
        const string json = """
            {
              "taskId": "abc123",
              "taskType": "GetCustomers",
              "parameters": { "modifiedSince": "2025-01-01T00:00:00Z" },
              "createdAt": "2025-01-01T00:00:00Z"
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, new StringContent(json, Encoding.UTF8, "application/json"));

        var result = await client.GetNextTaskAsync();

        Assert.NotNull(result);
        Assert.Equal("abc123", result!.TaskId);
        Assert.Equal(SyncTaskType.GetCustomers, result.TaskType);
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), result.Parameters?.ModifiedSince);
    }

    [Fact]
    public async Task GetNextTaskAsync_Throws_On401()
    {
        var client = CreateClient(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetNextTaskAsync());
    }

    [Fact]
    public async Task PostResultAsync_DoesNotThrow_On200()
    {
        var client = CreateClient(HttpStatusCode.OK, JsonContent.Create(new { accepted = true, taskId = "task-1" }));
        var result = SyncResult.Failed(MakeTask(), "irrelevant for this test");

        var exception = await Record.ExceptionAsync(() => client.PostResultAsync(result));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PostResultAsync_Throws_On401()
    {
        var client = CreateClient(HttpStatusCode.Unauthorized);
        var result = SyncResult.Failed(MakeTask(), "irrelevant for this test");

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostResultAsync(result));
    }

    [Fact]
    public async Task PostResultAsync_Throws_On400_AndIncludesPlatformErrorInMessage()
    {
        var client = CreateClient(
            HttpStatusCode.BadRequest,
            new StringContent("""{"accepted":false,"error":"Missing required field: taskId"}""", Encoding.UTF8, "application/json"));
        var result = SyncResult.Failed(MakeTask(), "irrelevant for this test");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostResultAsync(result));

        Assert.Contains("Missing required field: taskId", ex.Message);
    }
}

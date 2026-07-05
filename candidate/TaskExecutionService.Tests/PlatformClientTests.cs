using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TaskExecutionService.Platform;
using TaskExecutionService.Tests.TestDoubles;
using Xunit;

namespace TaskExecutionService.Tests;

public sealed class PlatformClientTests
{
    private static PlatformClient CreateClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5100") };
        return new PlatformClient(http, NullLogger<PlatformClient>.Instance);
    }

    [Fact]
    public async Task GetNextTask_NoContent_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        var task = await client.GetNextTaskAsync(CancellationToken.None);

        task.Should().BeNull();
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/sync/next-task");
    }

    [Fact]
    public async Task GetNextTask_Ok_DeserializesCamelCaseTask()
    {
        const string json = """
            {
              "taskId": "01JQFG8N3XRTV5KHW2YP4M7B6C",
              "taskType": "GetCustomers",
              "parameters": { "modifiedSince": "2025-01-01T00:00:00Z" },
              "createdAt": "2026-03-12T10:30:00Z"
            }
            """;
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, json));

        var task = await client.GetNextTaskAsync(CancellationToken.None);

        task.Should().NotBeNull();
        task!.TaskId.Should().Be("01JQFG8N3XRTV5KHW2YP4M7B6C");
        task.TaskType.Should().Be("GetCustomers");
        task.Parameters.Should().ContainKey("modifiedSince").WhoseValue.Should().Be("2025-01-01T00:00:00Z");
    }

    [Fact]
    public async Task GetNextTask_Unauthorized_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.Unauthorized));

        var act = () => client.GetNextTaskAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PostResult_Ok_SendsCamelCaseBodyWithNullErrorMessage()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{ "accepted": true, "taskId": "x" }""");
        var client = CreateClient(handler);
        var result = SyncResult.Completed(
            "01JQFG8N3XRTV5KHW2YP4M7B6C",
            "GetCustomers",
            new object[] { new { customerId = 11000 } },
            new DateTime(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc));

        await client.PostResultAsync(result, CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/sync/result");
        var body = handler.LastRequestBody!;
        body.Should().Contain("\"taskId\":");
        body.Should().Contain("\"status\":\"completed\"");
        body.Should().Contain("\"recordCount\":1");
        body.Should().Contain("\"errorMessage\":null"); // must be present even when completed
        body.Should().Contain("\"customerId\":11000");
    }

    [Fact]
    public async Task PostResult_BadRequest_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest, """{ "accepted": false, "error": "bad" }"""));
        var result = SyncResult.Failed("id", "GetCustomers", "boom", DateTime.UtcNow);

        var act = () => client.PostResultAsync(result, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}

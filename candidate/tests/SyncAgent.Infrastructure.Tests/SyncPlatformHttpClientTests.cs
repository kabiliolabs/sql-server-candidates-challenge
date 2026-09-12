using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Infrastructure.Http;
using Xunit;

namespace SyncAgent.Infrastructure.Tests;

public class SyncPlatformHttpClientTests
{
    [Fact]
    public async Task GetNextTaskAsync_With200Response_ReturnsDeserializedTask()
    {
        const string json = """
        {
          "taskId": "01TESTID000000000000000000",
          "taskType": "GetCustomers",
          "parameters": { "modifiedSince": "2025-01-01T00:00:00Z" },
          "createdAt": "2026-03-12T10:30:00Z"
        }
        """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetNextTaskAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("01TESTID000000000000000000", result!.TaskId);
        Assert.Equal("GetCustomers", result.TaskType);
        Assert.Equal("2025-01-01T00:00:00Z", result.Parameters["modifiedSince"]);
    }

    [Fact]
    public async Task GetNextTaskAsync_With204NoContent_ReturnsNullInsteadOfThrowing()
    {
        var client = CreateClient(HttpStatusCode.NoContent, string.Empty);

        var result = await client.GetNextTaskAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextTaskAsync_With401Unauthorized_ThrowsHttpRequestException()
    {
        var client = CreateClient(HttpStatusCode.Unauthorized, """{"error":"Unauthorized. Provide a valid X-Api-Key header."}""");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetNextTaskAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetNextTaskAsync_With500ServerError_ThrowsHttpRequestException()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, """{"error":"Internal server error"}""");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetNextTaskAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SubmitResultAsync_With200Accepted_ReturnsAcceptedTrue()
    {
        var client = CreateClient(HttpStatusCode.OK, """{"accepted":true,"taskId":"01TESTID000000000000000000"}""");

        var result = await client.SubmitResultAsync(
            "01TESTID000000000000000000", "GetCustomers", "completed", new List<object>(), 0,
            DateTime.UtcNow, null, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("01TESTID000000000000000000", result.TaskId);
    }

    [Fact]
    public async Task SubmitResultAsync_With400BadRequest_ReturnsAcceptedFalseWithoutThrowing()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, """{"accepted":false,"error":"Missing required field: taskId"}""");

        var result = await client.SubmitResultAsync(
            "", "GetCustomers", "completed", null, 0, DateTime.UtcNow, null, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("Missing required field: taskId", result.Error);
    }

    [Fact]
    public async Task SubmitResultAsync_ForCompletedTask_SendsEnvelopeMatchingTheContract()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"accepted":true,"taskId":"task-1"}""");
        var client = CreateClientWith(handler);

        await client.SubmitResultAsync(
            "task-1", "GetCustomers", "completed", new[] { new { customerId = 11000 } }, 1,
            new DateTime(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc), null, CancellationToken.None);

        var body = handler.LastRequestBody!;
        Assert.Contains("\"taskId\":\"task-1\"", body);
        Assert.Contains("\"taskType\":\"GetCustomers\"", body);
        Assert.Contains("\"status\":\"completed\"", body);
        Assert.Contains("\"recordCount\":1", body);
        Assert.Contains("\"executedAt\":\"2026-03-12T10:30:05Z\"", body);
        // The contract's completed-result example carries errorMessage as an explicit null.
        Assert.Contains("\"errorMessage\":null", body);
    }

    [Fact]
    public async Task SubmitResultAsync_ForFailedTask_SendsDataAsExplicitNull()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"accepted":true,"taskId":"task-2"}""");
        var client = CreateClientWith(handler);

        await client.SubmitResultAsync(
            "task-2", "GetOrders", "failed", null, 0,
            new DateTime(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc), "boom", CancellationToken.None);

        var body = handler.LastRequestBody!;
        Assert.Contains("\"status\":\"failed\"", body);
        Assert.Contains("\"data\":null", body);
        Assert.Contains("\"recordCount\":0", body);
        Assert.Contains("\"errorMessage\":\"boom\"", body);
    }

    private static SyncPlatformHttpClient CreateClient(HttpStatusCode statusCode, string responseBody) =>
        CreateClientWith(new FakeHttpMessageHandler(statusCode, responseBody));

    private static SyncPlatformHttpClient CreateClientWith(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5100") };
        return new SyncPlatformHttpClient(httpClient, NullLogger<SyncPlatformHttpClient>.Instance);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}

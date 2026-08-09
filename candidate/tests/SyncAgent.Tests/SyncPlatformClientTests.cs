using SyncAgent.Api;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Tests;

public class SyncPlatformClientTests
{
    [Fact]
    public async Task GetNextTaskAsync_ReturnsNull_WhenServerReturns204()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(
                HttpStatusCode.NoContent));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(
                "http://localhost:5100")
        };

        var client =
            new SyncPlatformClient(httpClient);

        // Act
        var result = await client.GetNextTaskAsync(
            CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextTaskAsync_DeserializesTask_WhenServerReturns200()
    {
        // Arrange
        const string json = """
            {
                "taskId": "test-123",
                "taskType": "GetProducts",
                "parameters": {
                    "modifiedSince": "2025-01-01T00:00:00Z"
                },
                "createdAt": "2026-08-09T12:00:00Z"
            }
            """;

        var response = new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content = new StringContent(
                json,
                System.Text.Encoding.UTF8,
                "application/json")
        };

        var handler =
            new FakeHttpMessageHandler(response);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(
                "http://localhost:5100")
        };

        var client =
            new SyncPlatformClient(httpClient);

        // Act
        var result = await client.GetNextTaskAsync(
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-123", result.TaskId);
        Assert.Equal("GetProducts", result.TaskType);
        Assert.Equal(
            new DateTimeOffset(
                2025, 1, 1,
                0, 0, 0,
                TimeSpan.Zero),
            result.Parameters.ModifiedSince);
    }

    private sealed class FakeHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(
            HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
using SyncAgent.Api;
using SyncAgent.Models;
using System;
using System.Net;
using System.Text.Json;
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

    [Fact]
    public async Task SubmitResultAsync_PostsSyncResult_WhenServerReturns200()
    {
        // Arrange
        var response = new HttpResponseMessage(
            HttpStatusCode.OK);

        var handler =
            new FakeHttpMessageHandler(response);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(
                "http://localhost:5100")
        };

        var client =
            new SyncPlatformClient(httpClient);

        var result = new SyncResult
        {
            TaskId = "t1",
            TaskType = "GetProducts",
            Status = "completed",
            Data = new object(),
            RecordCount = 5,
            ExecutedAt = new DateTimeOffset(
                2026, 8, 9, 12, 0, 0,
                TimeSpan.Zero),
            ErrorMessage = null
        };

        // Act
        await client.SubmitResultAsync(
            result,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            HttpMethod.Post,
            handler.Method);

        Assert.Equal(
            new Uri("http://localhost:5100/api/sync/result"),
            handler.RequestUri);

        Assert.NotNull(handler.RequestBody);

        var submitted = JsonSerializer.Deserialize<SyncResult>(
            handler.RequestBody,
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web));
        Assert.NotNull(submitted);
        Assert.Equal("t1", submitted.TaskId);
        Assert.Equal("GetProducts", submitted.TaskType);
        Assert.Equal("completed", submitted.Status);
        Assert.Equal(5, submitted.RecordCount);
    }

    [Fact]
    public async Task SubmitResultAsync_Throws_WhenServerReturnsError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(
                HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(
                "http://localhost:5100")
        };

        var client =
            new SyncPlatformClient(httpClient);

        // Act + Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SubmitResultAsync(
                new SyncResult(),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetNextTaskAsync_Throws_WhenServerReturnsError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(
                HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(
                "http://localhost:5100")
        };

        var client =
            new SyncPlatformClient(httpClient);

        // Act + Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetNextTaskAsync(
                CancellationToken.None));
    }

    [Fact]
    public async Task GetNextTaskAsync_Throws_WhenPayloadIsEmpty()
    {
        // Arrange
        var response = new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content = new StringContent(
                "",
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

        // Act + Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.GetNextTaskAsync(
                    CancellationToken.None));

        Assert.Contains(
            "empty task payload",
            exception.Message);
    }

    private sealed class FakeHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        public FakeHttpMessageHandler(
            HttpResponseMessage response)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            RequestBody = request.Content is not null
                ? await request.Content.ReadAsStringAsync(
                    cancellationToken)
                : null;

            return _response;
        }
    }
}
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TaskExecutionService.Data;
using TaskExecutionService.Configuration;
using TaskExecutionService.Platform;
using TaskExecutionService.Sync;
using TaskExecutionService.Sync.Executors;
using Microsoft.Extensions.Options;
using Xunit;

namespace TaskExecutionService.Tests;

/// <summary>
/// Full-loop check: a real in-process HTTP listener stands in for the platform,
/// the real <see cref="PlatformClient"/> fetches a task, the real dispatcher runs
/// the query against AdventureWorks, and the result is posted back and captured.
/// Opt-in via SYNC_DB_TESTS=1 (needs the database).
/// </summary>
[Trait("Category", "Integration")]
public sealed class EndToEndFlowTests
{
    private const string ApiKey = "candidate-test-key-2026";

    [SkippableFact]
    public async Task PollExecutePost_ProductInventory_PostsCompletedResult()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("SYNC_DB_TESTS") == "1", "Set SYNC_DB_TESTS=1 to run DB integration tests.");

        // Arrange a stub platform on a local port.
        const int port = 5199;
        var baseUrl = $"http://localhost:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        string? receivedApiKey = null;
        string? postedBody = null;
        var serverDone = Task.Run(async () =>
        {
            // 1) GET next-task -> hand out one GetProductInventory task
            var getCtx = await listener.GetContextAsync();
            receivedApiKey = getCtx.Request.Headers["X-Api-Key"];
            var task = new
            {
                taskId = "01JQFGCR5HKNW8PE3YMZQ6VB4T",
                taskType = "GetProductInventory",
                parameters = new { modifiedSince = "2000-01-01T00:00:00Z" },
                createdAt = "2026-03-12T10:33:00Z",
            };
            await WriteJson(getCtx.Response, 200, JsonSerializer.Serialize(task));

            // 2) POST result -> capture and accept
            var postCtx = await listener.GetContextAsync();
            using (var reader = new StreamReader(postCtx.Request.InputStream, postCtx.Request.ContentEncoding))
            {
                postedBody = await reader.ReadToEndAsync();
            }
            await WriteJson(postCtx.Response, 200, """{ "accepted": true, "taskId": "01JQFGCR5HKNW8PE3YMZQ6VB4T" }""");
        });

        // Real components wired the same way Program.cs wires them.
        var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        http.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        var client = new PlatformClient(http, NullLogger<PlatformClient>.Instance);

        var dbOptions = Options.Create(new DatabaseOptions
        {
            ConnectionString = Environment.GetEnvironmentVariable("SYNC_DB_CONNECTION")
                ?? "Server=localhost;Database=AdventureWorks2025;Integrated Security=True;Encrypt=False;TrustServerCertificate=True",
        });
        IDbConnectionFactory factory = new SqlConnectionFactory(dbOptions);
        var dispatcher = new TaskDispatcher(
            new ISyncTaskExecutor[] { new GetProductInventoryExecutor(factory) },
            TimeProvider.System,
            NullLogger<TaskDispatcher>.Instance);

        // Act: one full cycle.
        var fetched = await client.GetNextTaskAsync(CancellationToken.None);
        fetched.Should().NotBeNull();
        var result = await dispatcher.DispatchAsync(fetched!, CancellationToken.None);
        await client.PostResultAsync(result, CancellationToken.None);
        await serverDone;
        listener.Stop();

        // Assert: the platform saw the API key and a valid completed payload.
        receivedApiKey.Should().Be(ApiKey);
        postedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(postedBody!);
        var root = doc.RootElement;
        root.GetProperty("taskType").GetString().Should().Be("GetProductInventory");
        root.GetProperty("status").GetString().Should().Be("completed");
        root.GetProperty("recordCount").GetInt32().Should().BeGreaterThan(0);
        root.GetProperty("data").GetArrayLength().Should().Be(root.GetProperty("recordCount").GetInt32());
    }

    private static async Task WriteJson(HttpListenerResponse response, int status, string json)
    {
        response.StatusCode = status;
        response.ContentType = "application/json";
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }
}

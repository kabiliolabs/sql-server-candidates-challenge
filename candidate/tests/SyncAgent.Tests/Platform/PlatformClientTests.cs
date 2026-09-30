using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Handlers.Products;
using SyncAgent.Infrastructure.Platform;
using SyncAgent.Tests.TestDoubles;

namespace SyncAgent.Tests.Platform;

public sealed class PlatformClientTests
{
    private static readonly Uri BaseAddress = new("http://localhost:5100/");

    private static PlatformClient CreateClient(StubHttpMessageHandler stub)
        => new(new HttpClient(stub) { BaseAddress = BaseAddress }, NullLogger<PlatformClient>.Instance);

    // Same shape as docs/sample-payloads/next-task-get-customers.json
    private const string SampleTaskJson = """
        {
          "taskId": "01JQFG8N3XRTV5KHW2YP4M7B6C",
          "taskType": "GetCustomers",
          "parameters": { "modifiedSince": "2025-01-01T00:00:00Z" },
          "createdAt": "2026-03-12T10:30:00Z"
        }
        """;

    [Fact]
    public async Task No_content_means_no_pending_task()
    {
        var stub = StubHttpMessageHandler.Returning(HttpStatusCode.NoContent);

        var task = await CreateClient(stub).GetNextTaskAsync(CancellationToken.None);

        task.ShouldBeNull();
    }

    [Fact]
    public async Task Next_task_is_requested_from_the_contract_endpoint()
    {
        var stub = StubHttpMessageHandler.Returning(HttpStatusCode.NoContent);

        await CreateClient(stub).GetNextTaskAsync(CancellationToken.None);

        var request = stub.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.Uri.ShouldBe(new Uri("http://localhost:5100/api/sync/next-task"));
    }

    [Fact]
    public async Task Task_payload_is_deserialized_from_camel_case_json()
    {
        var stub = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SampleTaskJson);

        var task = await CreateClient(stub).GetNextTaskAsync(CancellationToken.None);

        task.ShouldNotBeNull();
        task.TaskId.ShouldBe("01JQFG8N3XRTV5KHW2YP4M7B6C");
        task.TaskType.ShouldBe(SyncTaskTypes.GetCustomers);
        task.Parameters.ShouldNotBeNull();
        task.Parameters.ModifiedSince.ShouldBe(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        task.Parameters.ModifiedSince!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Error_status_codes_throw_so_the_worker_can_back_off(HttpStatusCode statusCode)
    {
        var stub = StubHttpMessageHandler.Returning(statusCode);

        var ex = await Should.ThrowAsync<HttpRequestException>(
            () => CreateClient(stub).GetNextTaskAsync(CancellationToken.None));

        ex.StatusCode.ShouldBe(statusCode);
    }

    [Fact]
    public async Task Null_task_payload_throws()
    {
        var stub = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "null");

        await Should.ThrowAsync<JsonException>(() => CreateClient(stub).GetNextTaskAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_task_payload_throws()
    {
        var stub = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{ not json");

        await Should.ThrowAsync<JsonException>(() => CreateClient(stub).GetNextTaskAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Result_is_posted_to_the_contract_endpoint_with_camel_case_body()
    {
        var stub = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{ "accepted": true }""");
        var record = new ProductRecord
        {
            ProductId = 680,
            Name = "HL Road Frame - Black, 58",
            ProductNumber = "FR-R92B-58",
            // Dapper returns SQL datetime values with Kind = Unspecified
            ModifiedDate = new DateTime(2025, 2, 7, 10, 1, 36, 827, DateTimeKind.Unspecified)
        };
        var result = SyncResult.Completed(TestData.Task(), [record], TestData.Now.UtcDateTime);

        await CreateClient(stub).PostResultAsync(result, CancellationToken.None);

        var request = stub.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ShouldBe(new Uri("http://localhost:5100/api/sync/result"));

        using var body = JsonDocument.Parse(request.Body!);
        var root = body.RootElement;
        root.GetProperty("taskId").GetString().ShouldBe(TestData.ValidTaskId);
        root.GetProperty("taskType").GetString().ShouldBe(SyncTaskTypes.GetCustomers);
        root.GetProperty("status").GetString().ShouldBe("completed");
        root.GetProperty("recordCount").GetInt32().ShouldBe(1);
        root.GetProperty("executedAt").GetString().ShouldBe("2026-03-12T10:30:00Z");

        var product = root.GetProperty("data")[0];
        product.GetProperty("productId").GetInt32().ShouldBe(680);
        product.GetProperty("productNumber").GetString().ShouldBe("FR-R92B-58");
        product.GetProperty("modifiedDate").GetString().ShouldBe("2025-02-07T10:01:36.827Z");
    }

    [Fact]
    public async Task Failed_result_is_posted_with_null_data()
    {
        var stub = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{ "accepted": true }""");
        var result = SyncResult.Failed(TestData.Task(), "Unsupported taskType.", TestData.Now.UtcDateTime);

        await CreateClient(stub).PostResultAsync(result, CancellationToken.None);

        using var body = JsonDocument.Parse(stub.Requests.Single().Body!);
        var root = body.RootElement;
        root.GetProperty("status").GetString().ShouldBe("failed");
        root.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("recordCount").GetInt32().ShouldBe(0);
        root.GetProperty("errorMessage").GetString().ShouldBe("Unsupported taskType.");
    }

    [Fact]
    public async Task Rejected_result_throws()
    {
        var stub = StubHttpMessageHandler.Returning(
            HttpStatusCode.BadRequest, """{ "accepted": false, "error": "Missing required field: taskId" }""");
        var result = SyncResult.Failed(TestData.Task(), "error", TestData.Now.UtcDateTime);

        var ex = await Should.ThrowAsync<HttpRequestException>(
            () => CreateClient(stub).PostResultAsync(result, CancellationToken.None));

        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

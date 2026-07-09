using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;
using SyncAgent.Platform;
using SyncAgent.Platform.Contracts;
using SyncAgent.Tests.TestDoubles;

namespace SyncAgent.Tests.Platform;

public class PlatformClientTests
{
    private const string ApiKey = "candidate-test-key-2026";

    private readonly StubHttpMessageHandler _handler = new();
    private readonly RecordingLogger<PlatformClient> _logger = new();

    private PlatformClient CreateClient(string baseAddress = "http://localhost:5100")
    {
        var options = Options.Create(new PlatformOptions
        {
            ApiKey = ApiKey,
            BaseAddress = new Uri(baseAddress),
        });

        return new PlatformClient(new HttpClient(_handler), options, _logger);
    }

    private static string TaskJson(string taskId = "01JQFG8N3XRTV5KHW2YP4M7B6C", string taskType = "GetCustomers") => $$"""
        {
          "taskId": "{{taskId}}",
          "taskType": "{{taskType}}",
          "parameters": { "modifiedSince": "2025-01-01T00:00:00Z" },
          "createdAt": "2026-03-12T10:30:00Z"
        }
        """;

    [Fact]
    public async Task GetNextTaskAsync_deserialises_a_200_response_into_a_task()
    {
        _handler.RespondWith(HttpStatusCode.OK, TaskJson());

        var task = await CreateClient().GetNextTaskAsync(CancellationToken.None);

        task.Should().NotBeNull();
        task!.TaskId.Should().Be("01JQFG8N3XRTV5KHW2YP4M7B6C");
        task.TaskType.Should().Be("GetCustomers");
        task.Parameters.Should().ContainKey("modifiedSince").WhoseValue.Should().Be("2025-01-01T00:00:00Z");
        task.CreatedAt.Should().Be(new DateTimeOffset(2026, 3, 12, 10, 30, 0, TimeSpan.Zero));
    }

    // The empty-queue signal. Reading it as anything but "no task" would make the agent
    // treat an idle platform as a protocol error.
    [Fact]
    public async Task GetNextTaskAsync_returns_null_for_204_No_Content()
    {
        _handler.RespondWith(HttpStatusCode.NoContent);

        var task = await CreateClient().GetNextTaskAsync(CancellationToken.None);

        task.Should().BeNull();
    }

    [Fact]
    public async Task GetNextTaskAsync_returns_null_and_warns_when_a_200_body_deserialises_to_null()
    {
        _handler.RespondWith(HttpStatusCode.OK, "null");

        var task = await CreateClient().GetNextTaskAsync(CancellationToken.None);

        task.Should().BeNull();
        _logger.Entries.Should().Contain(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetNextTaskAsync_throws_on_a_non_success_status(HttpStatusCode statusCode)
    {
        _handler.RespondWith(statusCode);

        var act = () => CreateClient().GetNextTaskAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetNextTaskAsync_sends_the_api_key_header()
    {
        _handler.RespondWith(HttpStatusCode.NoContent);

        await CreateClient().GetNextTaskAsync(CancellationToken.None);

        _handler.Requests.Should().ContainSingle()
            .Which.ApiKeyHeaderValues.Should().Equal(ApiKey);
    }

    [Fact]
    public async Task GetNextTaskAsync_targets_the_documented_path()
    {
        _handler.RespondWith(HttpStatusCode.NoContent);

        await CreateClient().GetNextTaskAsync(CancellationToken.None);

        _handler.Requests.Single().Method.Should().Be(HttpMethod.Get);
        _handler.Requests.Single().RequestUri!.AbsoluteUri.Should().Be("http://localhost:5100/api/sync/next-task");
    }

    // Uri relative resolution drops the last path segment of a base address that has no trailing
    // slash, which would silently turn /agent into /api/sync/next-task at the site root.
    [Fact]
    public async Task GetNextTaskAsync_preserves_a_base_address_path_that_has_no_trailing_slash()
    {
        _handler.RespondWith(HttpStatusCode.NoContent);

        await CreateClient("http://localhost:5100/agent").GetNextTaskAsync(CancellationToken.None);

        _handler.Requests.Single().RequestUri!.AbsoluteUri
            .Should().Be("http://localhost:5100/agent/api/sync/next-task");
    }

    [Fact]
    public async Task PostResultAsync_sends_the_api_key_header_to_the_documented_path()
    {
        _handler.RespondWith(HttpStatusCode.OK, """{"accepted":true}""");

        await CreateClient().PostResultAsync(CompletedResult(), CancellationToken.None);

        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsoluteUri.Should().Be("http://localhost:5100/api/sync/result");
        request.ApiKeyHeaderValues.Should().Equal(ApiKey);
    }

    [Fact]
    public async Task PostResultAsync_throws_when_the_platform_rejects_the_result()
    {
        _handler.RespondWith(HttpStatusCode.BadRequest, """{"accepted":false,"error":"Missing required field: taskId"}""");

        var act = () => CreateClient().PostResultAsync(CompletedResult(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PostResultAsync_writes_the_contract_field_names_in_camelCase()
    {
        _handler.RespondWith(HttpStatusCode.OK, """{"accepted":true}""");

        await CreateClient().PostResultAsync(CompletedResult(), CancellationToken.None);

        using var document = JsonDocument.Parse(_handler.Requests.Single().Body!);
        var root = document.RootElement;

        root.GetProperty("taskId").GetString().Should().Be("T1");
        root.GetProperty("taskType").GetString().Should().Be("GetCustomers");
        root.GetProperty("status").GetString().Should().Be("completed");
        root.GetProperty("recordCount").GetInt32().Should().Be(1);
        root.GetProperty("executedAt").GetDateTime().Should().Be(new DateTime(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc));
        root.GetProperty("errorMessage").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
    }

    // SyncResult.Data is IReadOnlyList<object>. System.Text.Json only writes an element's real
    // properties when the declared element type is exactly `object`; declare it as anything else
    // and every row silently serialises to `{}`. The whole four-handlers-one-dispatcher design
    // rests on this, so it is pinned here rather than discovered against the live platform.
    [Fact]
    public async Task PostResultAsync_serialises_data_rows_by_their_runtime_type()
    {
        _handler.RespondWith(HttpStatusCode.OK, """{"accepted":true}""");

        await CreateClient().PostResultAsync(CompletedResult(), CancellationToken.None);

        using var document = JsonDocument.Parse(_handler.Requests.Single().Body!);
        var row = document.RootElement.GetProperty("data").EnumerateArray().Single();

        row.GetProperty("customerId").GetInt32().Should().Be(11000);
        row.GetProperty("firstName").GetString().Should().Be("Jon");
    }

    [Fact]
    public async Task PostResultAsync_writes_null_data_for_a_failed_result()
    {
        _handler.RespondWith(HttpStatusCode.OK, """{"accepted":true}""");

        var failed = SyncResult.Failed("T1", "GetCustomers", "Task execution failed.", new DateTime(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc));

        await CreateClient().PostResultAsync(failed, CancellationToken.None);

        using var document = JsonDocument.Parse(_handler.Requests.Single().Body!);
        document.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty("status").GetString().Should().Be("failed");
        document.RootElement.GetProperty("recordCount").GetInt32().Should().Be(0);
    }

    // The key is a secret. It belongs in a request header and nowhere else -- especially not in
    // the log the operator ships off the client machine.
    [Fact]
    public async Task The_api_key_never_reaches_the_log()
    {
        _handler
            .RespondWith(HttpStatusCode.OK, TaskJson())
            .RespondWith(HttpStatusCode.OK, """{"accepted":true}""");

        var client = CreateClient();
        await client.GetNextTaskAsync(CancellationToken.None);
        await client.PostResultAsync(CompletedResult(), CancellationToken.None);

        _logger.Entries.Should().NotBeEmpty("the client logs the posted result, so this assertion has something to search");
        _logger.AllText.Should().NotContain(ApiKey);
    }

    [Fact]
    public void Constructor_rejects_a_missing_dependency()
    {
        var options = Options.Create(new PlatformOptions { ApiKey = ApiKey });

        FluentActions.Invoking(() => new PlatformClient(null!, options, _logger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PlatformClient(new HttpClient(_handler), null!, _logger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PlatformClient(new HttpClient(_handler), options, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task PostResultAsync_rejects_a_null_result()
    {
        var act = () => CreateClient().PostResultAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static SyncResult CompletedResult() =>
        SyncResult.Completed(
            "T1",
            "GetCustomers",
            [new { CustomerId = 11000, FirstName = "Jon" }],
            new DateTime(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc));
}

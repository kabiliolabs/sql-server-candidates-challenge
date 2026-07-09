using System.Data.Common;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SyncAgent.Configuration;
using SyncAgent.Platform.Contracts;
using SyncAgent.Sync;
using SyncAgent.Tests.TestDoubles;
using SyncAgent.Validation;

namespace SyncAgent.Tests.Sync;

/// <summary>
/// Exercised against the real <see cref="TaskValidator"/> rather than a substitute: the two
/// collaborate to decide whether a task runs at all, and stubbing the validator would only assert
/// that the dispatcher calls a method.
/// </summary>
public class SyncTaskDispatcherTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 12, 10, 30, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly RecordingLogger<SyncTaskDispatcher> _logger = new();

    private SyncTaskDispatcher CreateDispatcher(IEnumerable<ISyncTaskHandler> handlers, int maxRecords = 5000) =>
        new(handlers, new TaskValidator(), Options.Create(new AgentOptions { MaxRecords = maxRecords }), _clock, _logger);

    private static SyncTask Task(string? taskId = "T1", string? taskType = "GetCustomers", string modifiedSince = "2025-01-01T00:00:00Z") =>
        new()
        {
            TaskId = taskId,
            TaskType = taskType,
            Parameters = new Dictionary<string, string> { ["modifiedSince"] = modifiedSince },
        };

    [Fact]
    public async Task Routes_a_task_to_the_handler_that_declares_its_type()
    {
        var customers = new RecordingSyncTaskHandler("GetCustomers") { Rows = [new object(), new object()] };
        var products = new RecordingSyncTaskHandler("GetProducts");

        var result = await CreateDispatcher([customers, products]).DispatchAsync(Task(taskType: "GetCustomers"), CancellationToken.None);

        customers.ExecuteCount.Should().Be(1);
        products.ExecuteCount.Should().Be(0);
        result!.Status.Should().Be("completed");
        result.RecordCount.Should().Be(2);
        result.TaskId.Should().Be("T1");
        result.TaskType.Should().Be("GetCustomers");
    }

    [Theory]
    [InlineData("getcustomers")]
    [InlineData("GETCUSTOMERS")]
    [InlineData("GetCustomers")]
    public async Task Routing_is_case_insensitive(string taskType)
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers");

        var result = await CreateDispatcher([handler]).DispatchAsync(Task(taskType: taskType), CancellationToken.None);

        handler.ExecuteCount.Should().Be(1);
        result!.Status.Should().Be("completed");
    }

    [Fact]
    public async Task Hands_the_handler_the_validated_modifiedSince_and_the_configured_cap()
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers");

        await CreateDispatcher([handler], maxRecords: 250)
            .DispatchAsync(Task(modifiedSince: "2025-06-01T12:00:00Z"), CancellationToken.None);

        handler.LastParameters.Should().Be(
            new SyncParameters(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero), 250));
    }

    // A taskType is only ever a dictionary key. An unrecognised one must never reach a query.
    [Fact]
    public async Task An_unknown_task_type_fails_without_executing_any_handler()
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers");

        var result = await CreateDispatcher([handler]).DispatchAsync(Task(taskType: "DropAllTables"), CancellationToken.None);

        handler.ExecuteCount.Should().Be(0);
        result!.Status.Should().Be("failed");
        result.TaskType.Should().Be("DropAllTables");
        result.ErrorMessage.Should().Contain("Unsupported task type");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task A_task_the_validator_rejects_fails_without_executing_any_handler()
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers");

        var result = await CreateDispatcher([handler]).DispatchAsync(Task(modifiedSince: "not-a-date"), CancellationToken.None);

        handler.ExecuteCount.Should().Be(0);
        result!.Status.Should().Be("failed");
        result.ErrorMessage.Should().Contain("modifiedSince");
    }

    // Nothing to report a failure against: the platform answers 400 to a result with no taskId.
    [Theory]
    [InlineData(null, "GetCustomers")]
    [InlineData("T1", null)]
    public async Task An_unaddressable_task_yields_no_result_to_post(string? taskId, string? taskType)
    {
        var result = await CreateDispatcher([]).DispatchAsync(Task(taskId, taskType), CancellationToken.None);

        result.Should().BeNull();
        _logger.Entries.Should().Contain(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error);
    }

    [Fact]
    public async Task A_null_task_yields_no_result_to_post()
    {
        (await CreateDispatcher([]).DispatchAsync(null, CancellationToken.None)).Should().BeNull();
    }

    // The failure boundary: one poisonous task must not take the always-on loop down with it.
    [Fact]
    public async Task A_handler_that_throws_becomes_a_failed_result_rather_than_an_exception()
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers")
        {
            ThrowOnExecute = new InvalidOperationException("Connection string: Server=.\\SQLEXPRESS;Password=hunter2"),
        };

        var result = await CreateDispatcher([handler]).DispatchAsync(Task(), CancellationToken.None);

        result!.Status.Should().Be("failed");
        result.Data.Should().BeNull();
        result.RecordCount.Should().Be(0);
    }

    [Fact]
    public async Task A_failed_result_carries_a_sanitised_message_and_the_detail_stays_in_the_local_log()
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers")
        {
            ThrowOnExecute = new FakeDbException("Login failed for user 'CORP\\svc_sync' on server '.\\SQLEXPRESS'."),
        };

        var result = await CreateDispatcher([handler]).DispatchAsync(Task(), CancellationToken.None);

        result!.ErrorMessage.Should().NotContain("svc_sync").And.NotContain("SQLEXPRESS");
        result.ErrorMessage.Should().Be("Database query failed on the client machine. See the agent log for details.");

        // The operator who owns the machine can already see everything, so the full exception
        // is logged locally. Only the category crosses the wire.
        _logger.AllText.Should().Contain("svc_sync");
    }

    // Shutdown is not a task failure. Reporting one would tell the platform something untrue.
    [Fact]
    public async Task Cancellation_during_shutdown_propagates_instead_of_becoming_a_failed_result()
    {
        using var cts = new CancellationTokenSource();
        var handler = new RecordingSyncTaskHandler("GetCustomers")
        {
            OnExecute = cts.Cancel,
            ThrowOnExecute = new OperationCanceledException(),
        };

        var act = () => CreateDispatcher([handler]).DispatchAsync(Task(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // A query timeout surfaces as an OperationCanceledException too, but with no shutdown pending.
    // That is a task failure and must be reported as one.
    [Fact]
    public async Task A_cancellation_with_no_shutdown_pending_is_reported_as_a_failed_task()
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers") { ThrowOnExecute = new OperationCanceledException() };

        var result = await CreateDispatcher([handler]).DispatchAsync(Task(), CancellationToken.None);

        result!.Status.Should().Be("failed");
    }

    // executedAt should say when the query ran, not when the task was validated.
    [Fact]
    public async Task ExecutedAt_is_read_after_the_handler_has_run()
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers")
        {
            OnExecute = () => _clock.Advance(TimeSpan.FromSeconds(42)),
        };

        var result = await CreateDispatcher([handler]).DispatchAsync(Task(), CancellationToken.None);

        result!.ExecutedAt.Should().Be(Start.UtcDateTime.AddSeconds(42));
        result.ExecutedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task A_failed_result_is_stamped_with_the_clock_too()
    {
        var handler = new RecordingSyncTaskHandler("GetCustomers")
        {
            OnExecute = () => _clock.Advance(TimeSpan.FromSeconds(7)),
            ThrowOnExecute = new TimeoutException(),
        };

        var result = await CreateDispatcher([handler]).DispatchAsync(Task(), CancellationToken.None);

        result!.ExecutedAt.Should().Be(Start.UtcDateTime.AddSeconds(7));
    }

    // Silently letting one handler shadow another would make a registration typo invisible.
    [Fact]
    public void Two_handlers_claiming_the_same_task_type_fail_at_construction()
    {
        var act = () => CreateDispatcher([new RecordingSyncTaskHandler("GetCustomers"), new RecordingSyncTaskHandler("getcustomers")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*GetCustomers*");
    }

    [Fact]
    public void Constructor_rejects_a_missing_dependency()
    {
        var options = Options.Create(new AgentOptions());

        FluentActions.Invoking(() => new SyncTaskDispatcher(null!, new TaskValidator(), options, _clock, _logger))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SyncTaskDispatcher([], null!, options, _clock, _logger))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SyncTaskDispatcher([], new TaskValidator(), null!, _clock, _logger))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SyncTaskDispatcher([], new TaskValidator(), options, null!, _logger))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SyncTaskDispatcher([], new TaskValidator(), options, _clock, null!))
            .Should().Throw<ArgumentNullException>();
    }

    private sealed class FakeDbException(string message) : DbException(message);
}

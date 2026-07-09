using FluentAssertions;
using SyncAgent.Platform.Contracts;
using SyncAgent.Validation;

namespace SyncAgent.Tests.Validation;

public class TaskValidatorTests
{
    private readonly TaskValidator _validator = new();

    private static SyncTask Task(string? taskId = "T1", string? taskType = "GetCustomers", string? modifiedSince = "2025-01-01T00:00:00Z")
    {
        var parameters = modifiedSince is null
            ? null
            : new Dictionary<string, string> { ["modifiedSince"] = modifiedSince };

        return new SyncTask { TaskId = taskId, TaskType = taskType, Parameters = parameters };
    }

    [Theory]
    [InlineData("2025-01-01T00:00:00Z")]
    [InlineData("2025-02-07T10:01:36.827Z")]
    [InlineData("2025-01-01T00:00:00+02:00")]
    [InlineData("2025-01-01T00:00:00.5-05:00")]
    public void Accepts_an_ISO_8601_instant_with_an_explicit_offset(string modifiedSince)
    {
        var result = _validator.Validate(Task(modifiedSince: modifiedSince));

        result.IsValid.Should().BeTrue();
        result.Outcome.Should().Be(TaskValidationOutcome.Valid);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Normalises_an_offset_instant_to_UTC()
    {
        var result = _validator.Validate(Task(modifiedSince: "2025-01-01T02:00:00+02:00"));

        result.ModifiedSince.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        result.ModifiedSince.Offset.Should().Be(TimeSpan.Zero);
    }

    // Without a taskId or taskType the platform answers 400 to any result we post, so there is
    // no failure the agent could usefully report. It can only log and drop the task.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_task_without_a_taskId_is_unaddressable(string? taskId)
    {
        var result = _validator.Validate(Task(taskId: taskId));

        result.Outcome.Should().Be(TaskValidationOutcome.Unaddressable);
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("taskId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_task_without_a_taskType_is_unaddressable(string? taskType)
    {
        var result = _validator.Validate(Task(taskType: taskType));

        result.Outcome.Should().Be(TaskValidationOutcome.Unaddressable);
        result.Error.Should().Contain("taskType");
    }

    [Fact]
    public void A_null_task_is_unaddressable()
    {
        _validator.Validate(null).Outcome.Should().Be(TaskValidationOutcome.Unaddressable);
    }

    // Identifiable but unusable: the agent can and should tell the platform this task failed.
    [Fact]
    public void A_task_with_no_parameters_at_all_is_rejected()
    {
        var result = _validator.Validate(Task(modifiedSince: null));

        result.Outcome.Should().Be(TaskValidationOutcome.Rejected);
        result.Error.Should().Contain("modifiedSince");
    }

    [Fact]
    public void A_task_missing_the_modifiedSince_parameter_is_rejected()
    {
        var task = new SyncTask
        {
            TaskId = "T1",
            TaskType = "GetCustomers",
            Parameters = new Dictionary<string, string> { ["somethingElse"] = "1" },
        };

        _validator.Validate(task).Outcome.Should().Be(TaskValidationOutcome.Rejected);
    }

    // TryParseExact against a closed format list, not the permissive culture-sensitive TryParse.
    // "1/1/2025" and "January 1, 2025" are not the documented contract and must not slip through.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    [InlineData("2025-01-01")]                       // date only, no time or offset
    [InlineData("2025-01-01T00:00:00")]              // no offset: ambiguous instant
    [InlineData("1/1/2025")]                         // culture-sensitive
    [InlineData("January 1, 2025")]
    [InlineData("2025-13-01T00:00:00Z")]             // month 13
    [InlineData("2025-01-01T00:00:00Z'; DROP TABLE Sales.Customer--")]
    [InlineData("2025-01-01T00:00:00Z OR 1=1")]
    public void An_unparseable_or_injection_shaped_modifiedSince_is_rejected(string modifiedSince)
    {
        var result = _validator.Validate(Task(modifiedSince: modifiedSince));

        result.Outcome.Should().Be(TaskValidationOutcome.Rejected);
        result.Error.Should().Contain("modifiedSince");
    }

    // The offending value is attacker-controlled. Echoing it back would reflect it straight into
    // the platform's log viewer.
    [Fact]
    public void The_rejection_message_does_not_echo_the_offending_value()
    {
        const string Hostile = "<script>alert(1)</script>'; DROP TABLE Sales.Customer--";

        var result = _validator.Validate(Task(modifiedSince: Hostile));

        result.Error.Should().NotContain("DROP TABLE").And.NotContain("<script>");
    }

    // The task-type allowlist lives in the dispatcher's routing table, not here: the set of
    // registered handlers *is* the allowlist, so there is no second list to keep in sync.
    [Fact]
    public void An_unknown_taskType_is_not_the_validators_business()
    {
        _validator.Validate(Task(taskType: "DropAllTables")).Outcome.Should().Be(TaskValidationOutcome.Valid);
    }
}

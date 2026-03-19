using FluentAssertions;
using SyncAgent.Contracts;
using SyncAgent.Validation;
using Xunit;

namespace SyncAgent.UnitTests;

public sealed class SyncTaskValidatorTests
{
    private readonly SyncTaskValidator _validator = new();

    [Fact]
    public void Validate_ShouldReturnFailure_WhenTaskTypeIsUnsupported()
    {
        var task = CreateTask(taskType: "UnknownTask");

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Unsupported task type: UnknownTask");
    }

    [Fact]
    public void Validate_ShouldReturnFailure_WhenTaskTypeIsMissing()
    {
        var task = CreateTask(taskType: string.Empty);

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("taskType");
    }

    [Fact]
    public void Validate_ShouldReturnSuccess_WhenTaskIsValid()
    {
        var task = CreateTask();

        var result = _validator.Validate(task);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    private static SyncTaskContract CreateTask(string taskType = "GetProducts", DateTimeOffset? modifiedSince = null) => new()
    {
        TaskId = "01JQFG8N3XRTV5KHW2YP4M7B6C",
        TaskType = taskType,
        Parameters = new SyncTaskParametersContract
        {
            ModifiedSince = modifiedSince ?? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        CreatedAt = new DateTimeOffset(2026, 3, 12, 10, 30, 0, TimeSpan.Zero)
    };
}

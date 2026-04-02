using SyncAgent.Core.Models;
using SyncAgent.Infrastructure;
using Xunit;

namespace SyncAgent.Tests.Services;

public class SyncTaskValidatorTests
{
    private readonly SyncTaskValidator _validator = new();

    [Fact]
    public void Validate_ReturnsFailure_WhenTaskIdIsMissing()
    {
        var result = _validator.Validate(new SyncTask
        {
            TaskType = "GetCustomers"
        });

        Assert.False(result.IsValid);
        Assert.Equal("Missing required field: taskId", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ReturnsFailure_WhenTaskTypeIsMissing()
    {
        var result = _validator.Validate(new SyncTask
        {
            TaskId = "task-1"
        });

        Assert.False(result.IsValid);
        Assert.Equal("Missing required field: taskType", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ReturnsFailure_WhenModifiedSinceIsInvalid()
    {
        var result = _validator.Validate(new SyncTask
        {
            TaskId = "task-1",
            TaskType = "GetCustomers",
            Parameters = new Dictionary<string, object?> { ["modifiedSince"] = "bad-date" }
        });

        Assert.False(result.IsValid);
        Assert.Equal("Invalid parameter: modifiedSince must be a valid date", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidTask()
    {
        var result = _validator.Validate(new SyncTask
        {
            TaskId = "task-1",
            TaskType = "GetCustomers",
            Parameters = new Dictionary<string, object?> { ["modifiedSince"] = "2025-01-01T00:00:00Z" }
        });

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }
}

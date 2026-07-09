using System.Data.Common;
using FluentAssertions;
using SyncAgent.Sync;

namespace SyncAgent.Tests.Sync;

public class ErrorSanitizerTests
{
    /// <summary>Everything a real provider exception would leak, in one string.</summary>
    private const string Leaky =
        "A network-related error occurred while establishing a connection to SQL Server " +
        "'.\\SQLEXPRESS', database 'AdventureWorks2025', user 'CORP\\svc_sync', password 'hunter2'.";

    public static TheoryData<Exception> LeakyExceptions =>
    [
        new FakeDbException(Leaky),
        new TimeoutException(Leaky),
        new InvalidOperationException(Leaky),
        new Exception(Leaky),
        new IOException(Leaky),
    ];

    [Theory]
    [MemberData(nameof(LeakyExceptions))]
    public void No_detail_from_the_original_exception_crosses_the_wire(Exception exception)
    {
        var sanitized = ErrorSanitizer.Sanitize(exception);

        sanitized.Should().NotContain("SQLEXPRESS");
        sanitized.Should().NotContain("AdventureWorks2025");
        sanitized.Should().NotContain("svc_sync");
        sanitized.Should().NotContain("hunter2");
        sanitized.Should().NotContain(exception.GetType().Name);
    }

    [Theory]
    [MemberData(nameof(LeakyExceptions))]
    public void Every_exception_yields_a_non_empty_message(Exception exception)
    {
        ErrorSanitizer.Sanitize(exception).Should().NotBeNullOrWhiteSpace();
    }

    // Matching the ADO.NET base class, not Microsoft.Data.SqlClient's SqlException, keeps the
    // failure boundary independent of the provider the agent happens to be built against.
    [Fact]
    public void A_provider_exception_is_reported_as_a_database_failure()
    {
        ErrorSanitizer.Sanitize(new FakeDbException(Leaky)).Should().Be(
            "Database query failed on the client machine. See the agent log for details.");
    }

    [Fact]
    public void A_timeout_is_reported_as_a_timeout()
    {
        ErrorSanitizer.Sanitize(new TimeoutException(Leaky)).Should().Be(
            "Database query timed out on the client machine.");
    }

    [Fact]
    public void An_invalid_operation_is_reported_as_an_agent_failure()
    {
        ErrorSanitizer.Sanitize(new InvalidOperationException(Leaky)).Should().Be(
            "Agent could not execute the task. See the agent log for details.");
    }

    [Fact]
    public void An_unrecognised_exception_falls_back_to_a_generic_message()
    {
        ErrorSanitizer.Sanitize(new IOException(Leaky)).Should().Be(
            "Task execution failed on the client machine. See the agent log for details.");
    }

    private sealed class FakeDbException(string message) : DbException(message);
}

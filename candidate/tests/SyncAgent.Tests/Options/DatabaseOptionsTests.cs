using Shouldly;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Tests.Options;

public sealed class DatabaseOptionsTests
{
    private static DatabaseOptions With(string connectionString) => new() { ConnectionString = connectionString };

    [Fact]
    public void Valid_connection_string_passes()
    {
        var options = With(@"Server=(localdb)\AW2025;Database=AdventureWorks2025;Integrated Security=True;TrustServerCertificate=True");

        OptionsValidation.Validate(options).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_connection_string_is_rejected_with_a_helpful_message(string connectionString)
    {
        var error = OptionsValidation.Validate(With(connectionString)).ShouldHaveSingleItem();

        error.ErrorMessage.ShouldNotBeNull();
        error.ErrorMessage.ShouldContain("ConnectionStrings:AdventureWorks");
    }

    [Fact]
    public void Malformed_connection_string_is_rejected()
    {
        var error = OptionsValidation.Validate(With("this is not a connection string")).ShouldHaveSingleItem();

        error.ErrorMessage.ShouldBe("Connection string 'AdventureWorks' has an invalid format.");
    }

    [Fact]
    public void Connection_string_without_database_is_rejected()
    {
        var error = OptionsValidation.Validate(With("Server=localhost;Integrated Security=True")).ShouldHaveSingleItem();

        error.ErrorMessage.ShouldBe("Connection string must specify a database (Database / Initial Catalog).");
    }

    [Fact]
    public void Connection_string_without_server_is_rejected()
    {
        var error = OptionsValidation.Validate(With("Database=AdventureWorks2025;Integrated Security=True")).ShouldHaveSingleItem();

        error.ErrorMessage.ShouldBe("Connection string must specify a server (Server / Data Source).");
    }

    [Fact]
    public void Validation_messages_never_contain_credentials()
    {
        var errors = OptionsValidation.Validate(With("Server=localhost;User ID=sync;Password=SuperSecret123!"));

        errors.ShouldNotBeEmpty(); // no database specified
        errors.ShouldAllBe(e => e.ErrorMessage == null || !e.ErrorMessage.Contains("SuperSecret123!"));
    }
}

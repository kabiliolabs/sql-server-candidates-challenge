using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using SyncAgent.Worker.Data;

namespace SyncAgent.Worker.Tests.Data;

public class SqlConnectionFactoryTests
{
    // Minimal IConfiguration/IConfigurationSection stand-ins, avoiding a dependency on
    // Microsoft.Extensions.Configuration.InMemory. GetConnectionString(name) actually
    // resolves via configuration.GetSection("ConnectionStrings")[name], not a direct
    // "ConnectionStrings:name" indexer lookup on the root - so GetSection needs a real
    // (if minimal) implementation, not just the indexer.
    private sealed class FakeConfiguration(string? connectionStringValue) : IConfiguration
    {
        public string? this[string key]
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => throw new NotSupportedException();

        public IConfigurationSection GetSection(string key) =>
            key == "ConnectionStrings"
                ? new ConnectionStringsSection(connectionStringValue)
                : throw new NotSupportedException($"Unexpected section requested: {key}");

        private sealed class ConnectionStringsSection(string? adventureWorksValue) : IConfigurationSection
        {
            public string? this[string key]
            {
                get => key == "AdventureWorks" ? adventureWorksValue : null;
                set => throw new NotSupportedException();
            }

            public string Key => "ConnectionStrings";
            public string Path => "ConnectionStrings";
            public string? Value
            {
                get => null;
                set => throw new NotSupportedException();
            }

            public IEnumerable<IConfigurationSection> GetChildren() => [];
            public IChangeToken GetReloadToken() => throw new NotSupportedException();
            public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenConnectionStringMissingOrBlank(string? value)
    {
        var configuration = new FakeConfiguration(value);

        var ex = Assert.Throws<InvalidOperationException>(() => new SqlConnectionFactory(configuration));

        Assert.Contains("ConnectionStrings:AdventureWorks", ex.Message);
    }

    [Fact]
    public void Constructor_Succeeds_WhenConnectionStringProvided()
    {
        var configuration = new FakeConfiguration("Server=localhost;Database=Test;");

        var exception = Record.Exception(() => new SqlConnectionFactory(configuration));

        Assert.Null(exception);
    }
}

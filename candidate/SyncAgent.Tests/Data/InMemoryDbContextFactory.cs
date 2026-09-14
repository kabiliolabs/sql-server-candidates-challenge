using Microsoft.EntityFrameworkCore;
using SyncAgent.Data;

namespace SyncAgent.Tests.Data;

/// <summary>
/// Helper for creating an isolated EF Core InMemory AdventureWorksDbContext per test.
/// </summary>
internal static class InMemoryDbContextFactory
{
    public static AdventureWorksDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AdventureWorksDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new AdventureWorksDbContext(options);
    }
}

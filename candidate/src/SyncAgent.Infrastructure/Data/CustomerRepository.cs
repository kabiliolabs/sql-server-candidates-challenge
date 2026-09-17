using Dapper;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Infrastructure.Data;

public sealed class CustomerRepository(ISqlConnectionFactory connectionFactory, IOptions<SqlOptions> sqlOptions)
    : ISyncQueryRepository<CustomerRecord>
{
    private readonly SqlOptions _sqlOptions = sqlOptions.Value;

    public async Task<IReadOnlyList<CustomerRecord>> GetModifiedSinceAsync(DateTimeOffset modifiedSince, int maxRows, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<CustomerRow>(
            new CommandDefinition(
                CustomerQueries.SelectModifiedSince,
                new { ModifiedSince = modifiedSince.UtcDateTime, Top = maxRows },
                commandTimeout: _sqlOptions.CommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return rows.Select(MapToRecord).ToList();
    }

    /// <summary>Explicit mapping step: AdventureWorks has no field matching the sample
    /// payload's "phone", so it's always null here — a deliberate, visible decision rather
    /// than a literal baked into the SQL text.</summary>
    private static CustomerRecord MapToRecord(CustomerRow row) => new(
        row.CustomerId,
        row.AccountNumber,
        row.FirstName,
        row.LastName,
        row.EmailAddress,
        Phone: null,
        row.AddressLine1,
        row.City,
        row.StateProvince,
        row.PostalCode,
        row.CountryRegion);
}

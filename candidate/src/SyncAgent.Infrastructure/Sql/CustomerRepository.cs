using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Application.Abstractions;
using SyncAgent.Domain.Models;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Sql;

public sealed class CustomerRepository(IOptions<DatabaseOptions> options, ILogger<CustomerRepository> logger)
    : ICustomerRepository
{
    // Only customers backed by a person are in scope: the contract's customer record is made of
    // person fields, and store-only customers (PersonID null) have none of them.
    // Address types 3 and 2 are Main Office and Home.
    private const string Sql = """
        SELECT
            c.CustomerID,
            c.AccountNumber,
            p.FirstName,
            p.LastName,
            ea.EmailAddress,
            pp.PhoneNumber,
            a.AddressLine1,
            a.City,
            sp.Name AS StateProvince,
            a.PostalCode,
            cr.Name AS CountryRegion
        FROM Sales.Customer c
        INNER JOIN Person.Person p ON c.PersonID = p.BusinessEntityID
        LEFT JOIN Person.EmailAddress ea ON p.BusinessEntityID = ea.BusinessEntityID
        LEFT JOIN Person.PersonPhone pp ON p.BusinessEntityID = pp.BusinessEntityID
        LEFT JOIN Person.BusinessEntityAddress bea ON p.BusinessEntityID = bea.BusinessEntityID AND bea.AddressTypeID IN (3, 2)
        LEFT JOIN Person.Address a ON bea.AddressID = a.AddressID
        LEFT JOIN Person.StateProvince sp ON a.StateProvinceID = sp.StateProvinceID
        LEFT JOIN Person.CountryRegion cr ON sp.CountryRegionCode = cr.CountryRegionCode
        WHERE c.ModifiedDate >= @ModifiedSince
        ORDER BY c.CustomerID
        """;

    private readonly DatabaseOptions _options = options.Value;

    public async Task<List<Customer>> GetCustomersAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        // A person with several e-mail addresses or addresses would repeat their customer row, so
        // the first row per CustomerID wins and the rest are ignored.
        var customers = new Dictionary<int, Customer>();

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(Sql, connection) { CommandTimeout = _options.CommandTimeoutSeconds };
        command.Parameters.Add("@ModifiedSince", SqlDbType.DateTime).Value = modifiedSince;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var customerId = reader.GetInt32(0);
            if (customers.ContainsKey(customerId))
            {
                continue;
            }

            customers[customerId] = new Customer
            {
                CustomerId = customerId,
                AccountNumber = reader.GetString(1),
                FirstName = reader.GetString(2),
                LastName = reader.GetString(3),
                EmailAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                Phone = reader.IsDBNull(5) ? null : reader.GetString(5),
                AddressLine1 = reader.IsDBNull(6) ? null : reader.GetString(6),
                City = reader.IsDBNull(7) ? null : reader.GetString(7),
                StateProvince = reader.IsDBNull(8) ? null : reader.GetString(8),
                PostalCode = reader.IsDBNull(9) ? null : reader.GetString(9),
                CountryRegion = reader.IsDBNull(10) ? null : reader.GetString(10)
            };
        }

        logger.LogInformation("Retrieved {CustomerCount} customers", customers.Count);
        return [.. customers.Values];
    }
}

using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.DTOs;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Handlers;

public class GetCustomersHandler : ISyncTaskHandler
{
    public string TaskType => "GetCustomers";

    private readonly string _connectionString;

    public GetCustomersHandler(IOptions<SyncAgentOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IEnumerable<object>> HandleAsync(
        TaskParameters parameters,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.CustomerID,
                c.AccountNumber,
                p.FirstName,
                p.LastName,
                e.EmailAddress,
                ph.PhoneNumber  AS Phone,
                a.AddressLine1,
                a.City,
                sp.Name         AS StateProvince,
                a.PostalCode,
                cr.Name         AS CountryRegion
            FROM Sales.Customer c
            INNER JOIN Person.Person p
                ON c.PersonID = p.BusinessEntityID
            LEFT JOIN Person.EmailAddress e
                ON p.BusinessEntityID = e.BusinessEntityID
            OUTER APPLY (
                SELECT TOP 1 PhoneNumber
                FROM Person.PersonPhone
                WHERE BusinessEntityID = p.BusinessEntityID
            ) ph
            OUTER APPLY (
                SELECT TOP 1 a2.AddressLine1, a2.City, a2.PostalCode, a2.StateProvinceID
                FROM Person.BusinessEntityAddress bea2
                INNER JOIN Person.Address a2 ON bea2.AddressID = a2.AddressID
                WHERE bea2.BusinessEntityID = p.BusinessEntityID
            ) a
            LEFT JOIN Person.StateProvince sp
                ON a.StateProvinceID = sp.StateProvinceID
            LEFT JOIN Person.CountryRegion cr
                ON sp.CountryRegionCode = cr.CountryRegionCode
            WHERE c.PersonID IS NOT NULL
              AND (@ModifiedSince IS NULL OR c.ModifiedDate >= @ModifiedSince)
            """;

        await using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<CustomerDto>(
            sql,
            new { parameters.ModifiedSince },
            commandTimeout: 60);

        return results.Cast<object>();
    }
}

using Dapper;
using SyncAgent.Worker.Data;
using SyncAgent.Worker.Platform;
using SyncAgent.Worker.Tasks.Models;

namespace SyncAgent.Worker.Tasks;

public class GetCustomersTaskHandler(ISqlConnectionFactory connectionFactory) : ISyncTaskHandler
{
    public SyncTaskType TaskType => SyncTaskType.GetCustomers;

    // OUTER APPLY (not a plain LEFT JOIN) for email/phone/address: a person can have
    // multiple rows in each of those tables (e.g. several phone types), which would
    // otherwise fan out into duplicate customer rows. TOP 1 per APPLY guarantees
    // exactly one output row per customer, matching the flat shape in
    // docs/sample-payloads/result-get-customers.json.
    private const string Sql = """
        SELECT
            c.CustomerID AS CustomerId,
            c.AccountNumber,
            p.FirstName,
            p.LastName,
            e.EmailAddress,
            ph.PhoneNumber AS Phone,
            a.AddressLine1,
            a.City,
            sp.Name AS StateProvince,
            a.PostalCode,
            cr.Name AS CountryRegion
        FROM Sales.Customer c
        INNER JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
        OUTER APPLY (
            SELECT TOP 1 EmailAddress
            FROM Person.EmailAddress
            WHERE BusinessEntityID = p.BusinessEntityID
            ORDER BY EmailAddressID
        ) e
        OUTER APPLY (
            SELECT TOP 1 PhoneNumber
            FROM Person.PersonPhone
            WHERE BusinessEntityID = p.BusinessEntityID
            ORDER BY PhoneNumber
        ) ph
        OUTER APPLY (
            SELECT TOP 1 a.AddressLine1, a.City, a.PostalCode, a.StateProvinceID
            FROM Person.BusinessEntityAddress bea
            INNER JOIN Person.Address a ON a.AddressID = bea.AddressID
            WHERE bea.BusinessEntityID = p.BusinessEntityID
            ORDER BY bea.AddressTypeID
        ) a
        LEFT JOIN Person.StateProvince sp ON sp.StateProvinceID = a.StateProvinceID
        LEFT JOIN Person.CountryRegion cr ON cr.CountryRegionCode = sp.CountryRegionCode
        WHERE (@ModifiedSince IS NULL OR c.ModifiedDate >= @ModifiedSince)
        """;

    public async Task<object> ExecuteAsync(TaskParameters? parameters, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            Sql,
            new { parameters?.ModifiedSince },
            cancellationToken: cancellationToken);

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var results = await connection.QueryAsync<CustomerResult>(command);
        return results.AsList();
    }
}

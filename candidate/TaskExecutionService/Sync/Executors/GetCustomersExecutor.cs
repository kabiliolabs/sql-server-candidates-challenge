using Dapper;
using TaskExecutionService.Data;
using TaskExecutionService.Models;

namespace TaskExecutionService.Sync.Executors;

/// <summary>
/// Returns individual customers (those linked to a Person) modified since the
/// given date. Email, phone and address are fetched with OUTER APPLY TOP 1 so a
/// person with several of each still yields exactly one row, preferring the Home
/// address (AddressTypeID = 2).
/// </summary>
public sealed class GetCustomersExecutor : ISyncTaskExecutor
{
    public const string Type = "GetCustomers";

    private const string Sql = """
        SELECT
            c.CustomerID       AS CustomerId,
            c.AccountNumber    AS AccountNumber,
            p.FirstName        AS FirstName,
            p.LastName         AS LastName,
            email.EmailAddress AS EmailAddress,
            phone.PhoneNumber  AS Phone,
            addr.AddressLine1  AS AddressLine1,
            addr.City          AS City,
            addr.StateProvince AS StateProvince,
            addr.PostalCode    AS PostalCode,
            addr.CountryRegion AS CountryRegion
        FROM Sales.Customer c
        INNER JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
        OUTER APPLY (
            SELECT TOP (1) ea.EmailAddress
            FROM Person.EmailAddress ea
            WHERE ea.BusinessEntityID = p.BusinessEntityID
            ORDER BY ea.EmailAddressID
        ) email
        OUTER APPLY (
            SELECT TOP (1) ph.PhoneNumber
            FROM Person.PersonPhone ph
            WHERE ph.BusinessEntityID = p.BusinessEntityID
            ORDER BY ph.PhoneNumberTypeID
        ) phone
        OUTER APPLY (
            SELECT TOP (1)
                a.AddressLine1,
                a.City,
                sp.Name AS StateProvince,
                a.PostalCode,
                cr.Name AS CountryRegion
            FROM Person.BusinessEntityAddress bea
            INNER JOIN Person.Address a ON a.AddressID = bea.AddressID
            INNER JOIN Person.StateProvince sp ON sp.StateProvinceID = a.StateProvinceID
            INNER JOIN Person.CountryRegion cr ON cr.CountryRegionCode = sp.CountryRegionCode
            WHERE bea.BusinessEntityID = p.BusinessEntityID
            ORDER BY CASE bea.AddressTypeID WHEN 2 THEN 0 ELSE 1 END, bea.AddressID
        ) addr
        WHERE c.PersonID IS NOT NULL
          AND c.ModifiedDate >= @modifiedSince
        ORDER BY c.CustomerID;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public GetCustomersExecutor(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public string TaskType => Type;

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            Sql,
            new { modifiedSince = parameters.ModifiedSince },
            commandTimeout: _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<CustomerDto>(command);
        return rows.ToArray();
    }
}

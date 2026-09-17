namespace SyncAgent.Infrastructure.Data;

internal static class CustomerQueries
{
    /// <summary>
    /// Individual customers only (inner join to Person): of the customers matching the
    /// ModifiedDate filter, ~700 are store/B2B accounts with no linked Person row (no name,
    /// email, or address) — out of scope, since the sample payload has no shape for a
    /// business account.
    ///
    /// Note there's no "phone" column here at all — AdventureWorks has no field matching the
    /// sample payload's phone. That's handled as an explicit mapping decision in
    /// CustomerRepository.MapToRecord, not hidden inside this query as a literal NULL.
    ///
    /// Dedupe: a small number of customers (~24) have more than one address row in
    /// BusinessEntityAddress. ROW_NUMBER() picks one deterministically, preferring the
    /// "Home" address type, to avoid duplicate customer rows in the result.
    /// </summary>
    public const string SelectModifiedSince = """
        WITH RankedAddress AS (
            SELECT
                bea.BusinessEntityID,
                bea.AddressID,
                ROW_NUMBER() OVER (
                    PARTITION BY bea.BusinessEntityID
                    ORDER BY CASE WHEN bea.AddressTypeID = 2 THEN 0 ELSE 1 END, bea.AddressID
                ) AS RowNum
            FROM Person.BusinessEntityAddress bea
        )
        SELECT TOP (@Top)
            c.CustomerID       AS CustomerId,
            c.AccountNumber,
            p.FirstName,
            p.LastName,
            e.EmailAddress,
            a.AddressLine1,
            a.City,
            sp.Name             AS StateProvince,
            a.PostalCode,
            cr.Name             AS CountryRegion
        FROM Sales.Customer c
        INNER JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
        LEFT JOIN Person.EmailAddress e ON e.BusinessEntityID = p.BusinessEntityID
        LEFT JOIN RankedAddress ra ON ra.BusinessEntityID = p.BusinessEntityID AND ra.RowNum = 1
        LEFT JOIN Person.Address a ON a.AddressID = ra.AddressID
        LEFT JOIN Person.StateProvince sp ON sp.StateProvinceID = a.StateProvinceID
        LEFT JOIN Person.CountryRegion cr ON cr.CountryRegionCode = sp.CountryRegionCode
        WHERE c.ModifiedDate >= @ModifiedSince
        ORDER BY c.CustomerID;
        """;
}

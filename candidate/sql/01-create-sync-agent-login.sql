/*
    Creates the least-privilege principal the sync agent should run as.

    The agent only ever reads, and it only ever reads seventeen tables. Running it as sa, or
    even as db_datareader, would give a process that talks to the public internet the ability
    to read every row of a client's business database — including HumanResources.Employee,
    Person.Password and Sales.CreditCard, none of which any sync task touches.

    So this grants SELECT on exactly the tables the four queries name, and nothing else.
    If a future task type needs another table, this script has to be edited: that is the
    point. The permission set is a visible, reviewable list rather than a blanket role.

    Run once per client database, as an administrator:

        sqlcmd -S .\SQLEXPRESS2025 -E -d AdventureWorks2025 -i 01-create-sync-agent-login.sql \
               -v Password="<a strong password>"

    Then point the agent's connection string at it:

        Server=.\SQLEXPRESS2025;Database=AdventureWorks2025;User ID=kabilio_sync_agent;
        Password=<the same>;Encrypt=True;TrustServerCertificate=True

    Integrated Security is the better option where the agent runs as a domain service
    account: create the login FROM WINDOWS instead and skip the password entirely.
*/

SET NOCOUNT ON;
GO

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'kabilio_sync_agent')
BEGIN
    -- CHECK_POLICY enforces the Windows password policy on this login.
    CREATE LOGIN [kabilio_sync_agent]
        WITH PASSWORD = N'$(Password)',
             CHECK_POLICY = ON,
             CHECK_EXPIRATION = OFF,
             DEFAULT_DATABASE = [AdventureWorks2025];
END
GO

USE [AdventureWorks2025];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'kabilio_sync_agent')
BEGIN
    CREATE USER [kabilio_sync_agent] FOR LOGIN [kabilio_sync_agent];
END
GO

/*
    Explicitly deny the two blanket read roles. Without this, someone adding the agent's user
    to db_datareader "to fix a permissions error" would silently widen its reach back to the
    whole database, and nothing would fail to warn them.
*/
DENY SELECT ON SCHEMA::[HumanResources] TO [kabilio_sync_agent];
DENY SELECT ON SCHEMA::[Purchasing]    TO [kabilio_sync_agent];
GO

-- GetCustomers
GRANT SELECT ON [Sales].[Customer]                TO [kabilio_sync_agent];
GRANT SELECT ON [Person].[Person]                 TO [kabilio_sync_agent];
GRANT SELECT ON [Person].[BusinessEntityAddress]  TO [kabilio_sync_agent];
GRANT SELECT ON [Person].[Address]                TO [kabilio_sync_agent];
GRANT SELECT ON [Person].[AddressType]            TO [kabilio_sync_agent];
GRANT SELECT ON [Person].[StateProvince]          TO [kabilio_sync_agent];
GRANT SELECT ON [Person].[CountryRegion]          TO [kabilio_sync_agent];
GRANT SELECT ON [Person].[EmailAddress]           TO [kabilio_sync_agent];
GRANT SELECT ON [Person].[PersonPhone]            TO [kabilio_sync_agent];
GO

-- GetProducts and GetProductInventory
GRANT SELECT ON [Production].[Product]            TO [kabilio_sync_agent];
GRANT SELECT ON [Production].[ProductSubcategory] TO [kabilio_sync_agent];
GRANT SELECT ON [Production].[ProductCategory]    TO [kabilio_sync_agent];
GRANT SELECT ON [Production].[ProductInventory]   TO [kabilio_sync_agent];
GRANT SELECT ON [Production].[Location]           TO [kabilio_sync_agent];
GO

-- GetOrders
GRANT SELECT ON [Sales].[SalesOrderHeader]        TO [kabilio_sync_agent];
GRANT SELECT ON [Sales].[SalesOrderDetail]        TO [kabilio_sync_agent];
GRANT SELECT ON [Sales].[Store]                   TO [kabilio_sync_agent];
GO

PRINT 'kabilio_sync_agent created with SELECT on the 17 tables the sync queries use.';
GO

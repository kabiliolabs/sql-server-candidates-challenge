# SyncAgent

An always-on .NET Worker Service that polls the SyncPlatform for sync tasks, runs the matching
query against a local AdventureWorks database, and posts the results back.

## Requirements

- .NET 10 SDK (pinned to 10.0.401 in `global.json`)
- SQL Server with the `AdventureWorks2025` database restored
- The SyncPlatform test app running on `http://localhost:5100`

## Running

```bash
cd candidate
dotnet restore
dotnet build
dotnet run --project src/SyncAgent.Worker
```

The agent polls every 10 seconds, logs each task it receives, and keeps running when the platform
or the database is unavailable.

### Tests

```bash
# Unit tests only - no external dependencies
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Everything, including tests that query the real AdventureWorks2025 database
dotnet test
```

`SyncAgent.Infrastructure.IntegrationTests` needs SQL Server reachable at the connection string in
`RepositoryTests`/`SyncPipelineTests`; the other four test projects have no external dependencies.

## Configuration

`src/SyncAgent.Worker/appsettings.json`:

```json
{
  "SyncPlatform": {
    "BaseUrl": "http://localhost:5100",
    "ApiKey": "candidate-test-key-2026",
    "TimeoutSeconds": 30
  },
  "Database": {
    "ConnectionString": "Server=localhost\\SQLEXPRESS;Database=AdventureWorks2025;Trusted_Connection=True;TrustServerCertificate=True;",
    "CommandTimeoutSeconds": 30
  },
  "Polling": {
    "IntervalSeconds": 10
  }
}
```

Every setting can be overridden by environment variables, for example
`SyncPlatform__BaseUrl=http://localhost:5199`. All three sections are validated at startup: an
invalid URL, a missing key or a non-positive interval stops the host immediately, naming the
setting, instead of failing later during polling.

The connection string uses Windows authentication, so no password is stored.
`TrustServerCertificate=True` is required because SQL Server encrypts connections by default and a
local instance uses a self-signed certificate.

## Structure

```
src/
  SyncAgent.Domain          entities, task parameters, validation - no dependencies
  SyncAgent.Application     use cases: handlers, dispatcher, poller, contracts, abstractions
  SyncAgent.Infrastructure  SQL repositories, HTTP client, options, DI registration
  SyncAgent.Worker          host, polling loop, configuration
tests/
  SyncAgent.Domain.Tests
  SyncAgent.Application.Tests
  SyncAgent.Infrastructure.Tests             fake HTTP handler, DI composition
  SyncAgent.Infrastructure.IntegrationTests  real AdventureWorks queries and full pipeline
  SyncAgent.Worker.Tests
```

References point inward: Worker depends on Infrastructure, Infrastructure on Application,
Application on Domain, and Domain on nothing.

## How a task is handled

`SyncAgentWorker` ticks on a `PeriodicTimer` and opens a DI scope per cycle. `SyncTaskPoller` calls
`GET /api/sync/next-task`; `204 No Content` means idle. A task is routed by `SyncTaskDispatcher` to
the `ISyncTaskHandler` whose `TaskType` matches, the handler validates `modifiedSince`, queries its
repository and maps rows to the contract records, and the poller posts the outcome to
`POST /api/sync/result`.

Adding a task type means adding a handler that derives from `SyncTaskHandlerBase<TRecord>` and
registering it in `ServiceCollectionExtensions`. Nothing else changes.

## Tables per task

| Task | AdventureWorks tables |
|------|-----------------------|
| `GetCustomers` | `Sales.Customer` → `Person.Person`, plus `EmailAddress`, `PersonPhone`, `BusinessEntityAddress` → `Address` → `StateProvince` → `CountryRegion` |
| `GetProducts` | `Production.Product` left-joined to `ProductSubcategory` → `ProductCategory` |
| `GetOrders` | `Sales.SalesOrderHeader` → `Customer` → `Person`, `SalesOrderDetail` → `Production.Product` |
| `GetProductInventory` | `Production.ProductInventory` → `Product`, `Location` |

Each query filters on its own table's `ModifiedDate` with a single `@ModifiedSince` parameter.

## Running as a Windows Service

`Program.cs` calls `AddWindowsService`, which is a no-op when started from a console. To install:

```powershell
sc.exe create SyncAgent binPath= "C:\path\to\SyncAgent.Worker.exe"
sc.exe start SyncAgent
```

## Notes

- AdventureWorks seed data has no `ModifiedDate` later than 2014, while the SyncPlatform buttons
  send `modifiedSince = 2025-01-01`. Every task therefore completes with `recordCount: 0` against an
  unmodified database. That is the filter working, not a failure. Lowering the date in
  `TaskQueueService` returns real data (19,119 customers, 504 products, 31,465 orders, 1,069
  inventory rows).
- One task is processed per cycle, in order. The contract hands out a single task per request and
  says nothing about parallelism, so the agent does not add any.
- Results are sent in one payload; the contract has no pagination. The largest case, `GetOrders`
  over the full history, is about 19 MB and completes in roughly five seconds.
- A failed poll is logged and retried on the next tick. There is no retry or backoff beyond that,
  because the contract does not define retry semantics.

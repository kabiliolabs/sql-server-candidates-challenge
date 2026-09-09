# Kabilio SQL Server Sync Agent

An always-on agent that polls the central platform for sync tasks, runs them against a local
AdventureWorks database, and reports the results.

Built on **.NET Framework 4.8**, as a single executable that runs either as a console
application or as a Windows Service.

---

## Quick start

```bash
# From the repository root
dotnet build candidate/SyncAgent.sln -c Release
dotnet test  candidate/SyncAgent.sln -c Release

# Terminal 1 — the provided test platform
dotnet run --project src/SyncPlatform/SyncPlatform/SyncPlatform.csproj

# Terminal 2 — the agent
candidate\src\SyncAgent.Host\bin\Release\net48\SyncAgent.Host.exe --console
```

Then click **Sync Customers / Products / Orders / Inventory** in the platform window. Each
click should produce `RESPONSE 200 OK — Result accepted` in its log pane.

Before the first run, point the connection string in
[`src/SyncAgent.Host/App.config`](src/SyncAgent.Host/App.config) at your SQL Server instance,
or set `SYNCAGENT_CONNECTIONSTRING`.

> `dotnet build` compiles `net48` without Visual Studio: the solution references
> `Microsoft.NETFramework.ReferenceAssemblies`, so the .NET SDK alone is enough.

---

## What it does

```
  GET /api/sync/next-task ──▶ validate ──▶ dispatch to handler ──▶ query SQL Server
                                                                        │
  POST /api/sync/result   ◀────────────── build result ◀────────────────┘
```

Four task types are supported: `GetCustomers`, `GetProducts`, `GetOrders`,
`GetProductInventory`. Each maps to one parameterised query and one payload shape, matching
[`docs/sample-payloads/`](../docs/sample-payloads/).

---

## Project layout

| Project | Role |
|---|---|
| `SyncAgent.Core` | Domain model, abstractions, dispatcher, validation, retry, the polling loop. References no I/O library. |
| `SyncAgent.Infrastructure` | ADO.NET queries, HTTP platform client, JSON contract, configuration. |
| `SyncAgent.Host` | Composition root, console/Windows Service entry point, `App.config`, logging. |
| `SyncAgent.Core.Tests` | 85 unit tests. No database, no sockets, under a second. |
| `SyncAgent.Integration.Tests` | 11 tests against a real AdventureWorks. Skipped unless opted in. |

The dependency direction is enforced by project references: `Core` knows nothing about SQL
Server, HTTP, Windows services or configuration files. That is what makes the polling loop
testable in milliseconds.

---

## Adding a new task type

1. Write a query class in `Infrastructure/Data/Queries/` and its interface in
   `Core/Abstractions/`.
2. Write a handler deriving from `ModifiedSinceSyncTaskHandler<TRecord>` — a task type name
   and a query call, about twenty lines.
3. Add one line to `CompositionRoot`:

```csharp
services.AddSingleton<ISyncTaskHandler, GetSuppliersHandler>();
```

Nothing else changes. The dispatcher builds its routing table from whatever handlers the
container hands it, and the worker and platform client never learn that a new type exists.

---

## Running as a Windows Service

```powershell
# From an elevated prompt, in the build output directory
SyncAgent.Host.exe install
net start KabilioSyncAgent
net stop  KabilioSyncAgent
SyncAgent.Host.exe uninstall
```

The service runs as `NetworkService`, which presents the machine account to SQL Server. Grant
that account access to the database, or switch the connection string to SQL authentication
using the least-privilege login created by
[`sql/01-create-sync-agent-login.sql`](sql/01-create-sync-agent-login.sql).

Logs are written to `logs/sync-agent-<date>.log` beside the executable, in both modes.

---

## Configuration

Everything lives in `App.config`. Defaults are chosen to be safe on an unattended client
machine, and are validated at startup — the agent refuses to start on bad configuration
rather than failing one task at a time.

| Setting | Default | Purpose |
|---|---|---|
| `Platform:BaseUrl` | `http://localhost:5100` | Platform address |
| `Platform:ApiKey` | `candidate-test-key-2026` | Sent as `X-Api-Key` |
| `Agent:PollIntervalSeconds` | 5 | Poll cadence when there is work |
| `Agent:MaxIdleBackoffSeconds` | 60 | Ceiling for the idle backoff |
| `Agent:MaxRecordsPerTask` | 50000 | Row cap, applied inside the SQL |
| `Agent:MaxResultPayloadBytes` | 33554432 | Refuses an oversized result before sending |
| `Agent:SqlCommandTimeoutSeconds` | 30 | Per-query timeout |
| `Agent:HttpTimeoutSeconds` | 100 | Per-request timeout |
| `Agent:MaxRetryAttempts` | 4 | Total attempts on a transient failure |
| `Agent:MinModifiedSince` | `2000-01-01T00:00:00Z` | Refuses a cutoff that would force a full scan |
| `Agent:ClockSkewToleranceSeconds` | 300 | Allowed disagreement between the two clocks |
| `Agent:ShutdownGracePeriodSeconds` | 10 | Time to report an interrupted task |

**Secrets.** `SYNCAGENT_PLATFORM_APIKEY` and `SYNCAGENT_CONNECTIONSTRING` override the file
and take precedence. Alternatively both sections can be encrypted at rest with DPAPI, with no
code change:

```powershell
aspnet_regiis -pef "connectionStrings" "<folder containing SyncAgent.Host.exe.config>"
```

---

## Tests

```bash
# Unit tests only — integration tests skip themselves
dotnet test candidate/SyncAgent.sln -c Release

# Including integration tests
$env:SYNCAGENT_TEST_CONNECTIONSTRING = "Server=.\SQLEXPRESS2025;Database=AdventureWorks2025;Integrated Security=true;Encrypt=True;TrustServerCertificate=True"
dotnet test candidate/SyncAgent.sln -c Release
```

The integration tests assert against the fixed identifiers in the sample payloads — customer
11000, product 680, order 43659, inventory for product 1 at Tool Crib — so they check the
mapping against the documented contract rather than against whatever the query returns today.

---

## Notes on the platform's API

Three properties of the provided platform shaped the design, and are worth stating because
none of them are in `api-contract.md`:

**The result body must be camelCase.** The platform deserialises with
`JsonSerializer.Deserialize<SyncResult>(body)` and no options, so `PropertyNameCaseInsensitive`
is false while its model uses lowercase `[JsonPropertyName]`. A body with default .NET naming
binds nothing: deserialisation succeeds, every field stays empty, and the agent is told
`400 Missing required field: taskId` about a body that visibly contains one. `JsonContractTests`
exists to keep that from ever regressing.

**The dequeue is destructive.** `GET /api/sync/next-task` removes the task with no ack, no
lease and no redelivery, so from the moment it returns, the agent holds the only copy. Every
path through the worker therefore ends in a submitted result, shutdown included, and a single
instance is enforced by mutex so two agents cannot silently split the queue.

**The listener binds `localhost`, not `+`.** Calling `http://127.0.0.1:5100` is rejected by
http.sys before it reaches the handler.

---

## Known limitations

- **Truncation is invisible to the platform.** The contract has no field for it, so a sync
  clipped by `MaxRecordsPerTask` is reported as a normal completion. The agent logs a warning
  locally; a real deployment would want a `hasMore` flag and a continuation token.
- **The two statements of the orders query are not in one transaction.** A line inserted
  between them arrives on the next sync. Snapshot isolation would close the gap on a database
  with `READ_COMMITTED_SNAPSHOT` enabled.
- **Customers with no associated person are not synced.** 701 of 19,820 rows in
  `Sales.Customer` are store accounts with a null `PersonID`, and the contract's record shape
  requires a first and last name. This is a question for whoever owns the contract, not a bug
  to work around silently.
- **The Windows Service lifecycle was not exercised end to end** on the development machine.
  The code paths are conventional `ServiceBase` and `ManagedInstallerClass` usage and the
  build is verified, but `install` / `net start` / `net stop` were not run.

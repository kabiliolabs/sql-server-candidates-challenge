# SyncAgent — Implementation Plan (final)

Client-side sync agent for the SQL Server Candidates Challenge. Polls the central
platform (`SyncPlatform`) for tasks, executes them against AdventureWorks2025, and
posts results back.

## Stack

| Concern | Choice | Why |
|---|---|---|
| Target framework | .NET Framework 4.8 (SDK-style project, built with .NET 10 SDK + ReferenceAssemblies) | Challenge asks to show experience with older technology; net48 output runs on the 4.8 runtime installed on this machine |
| Language | C# 7.3 | Pinned to what net48-era C# supported; no compiler features newer than the runtime era |
| DI / config / logging | Microsoft.Extensions.* 8.0.x | Standard, works on net462+ |
| HTTP | `HttpClient` via `Microsoft.Extensions.Http` 8.0.1 | Typed client + factory wiring |
| JSON | Newtonsoft.Json 13.0.3 | Payloads are camelCase; Newtonsoft is the natural net48-era choice |
| SQL access | `System.Data.SqlClient` 4.8.6 | Deliberate: net48-era ADO.NET driver (not Microsoft.Data.SqlClient); parameterized queries only |
| Tests | NUnit 3.14 + NUnit3TestAdapter + Microsoft.NET.Test.Sdk | Chosen over xUnit; fits the target-framework story |

## Layout

```
candidate/
  SyncAgent.sln
  SyncAgent/
    Program.cs                 dual-mode entry: --console / interactive -> console, else Windows Service
    SyncAgentService.cs        ServiceBase lifecycle (OnStart/OnStop/OnShutdown)
    SyncAgentWorker.cs         poll loop: get -> dedupe -> dispatch -> post; per-iteration fault isolation
    AgentOptions.cs            options POCO + Validate() (fail fast)
    appsettings.json           committed placeholders
    appsettings.Local.json     gitignored, real credentials
    Contracts/SyncRows.cs      result row DTOs (payload-exact camelCase)
    Domain/SyncTask.cs         task model (taskId, taskType, parameters.modifiedSince, createdAt)
    Domain/SyncResult.cs       result model + Completed/Failed factories
    Protocol/ISyncTaskHandler.cs      strategy interface (one impl per task type)
    Protocol/SyncTaskDispatcher.cs    handler lookup by taskType; unknown/failed -> "failed" result
    Protocol/SyncPlatformClient.cs    typed HttpClient: next-task / post-result, 204 = no work
    Sql/SqlConnectionFactory.cs       holds conn string; new SqlConnection per call (pooled)
    Sql/SqlReaderExtensions.cs        GetStringOrNull helper
    Handlers/                         1 handler per task type (customers, products, orders, inventory)
  SyncAgent.Tests/
    DispatcherTests.cs / PlatformClientTests.cs / WorkerLoopTests.cs / ContractShapeTests.cs
    Integration/HandlerIntegrationTests.cs   [Category("Integration")] real LocalDB
```

## Key decisions

- **Strategy over switch.** Each task type is an `ISyncTaskHandler`; the dispatcher
  maps `taskType -> handler` at startup. Adding a task type = new class + DI registration.
- **204 = success, not error.** `GetNextTaskAsync` returns `null` on 204; the worker
  sleeps `PollIntervalMs` and polls again. Never logged as a failure.
- **`modifiedSince` is parameterized.** `@ModifiedSince` with `SqlDbType.DateTime2`,
  `DBNull` when absent — no string concatenation, no injection surface.
- **Row cap + command timeout.** Every query uses `TOP (@MaxRows)` and a 30 s command
  timeout to prevent runaway queries.
- **Fault isolation.** The worker wraps the whole iteration (get → dispatch → post) in
  try/catch: one bad task, handler, or network blip logs and continues; the process
  never dies.
- **Idempotency (forward-looking).** Tasks are deduped by `taskId` in a `HashSet`.
  The current SyncPlatform mints a fresh ULID per task (no redelivery), so this is
  defense-in-depth for future redelivery semantics.
- **Fail fast on bad config.** Options are validated in `Program` before the service
  starts / the console loop begins; invalid config writes the event log and dies
  instead of silently polling with bad settings.
- **401 handling.** Wrong/missing API key throws in the client and surfaces as a
  logged error; the loop keeps polling (so a transient key rotation recovers without
  a restart).

## Handlers (all parameterized, verified against the AdventureWorks schema)

- `GetCustomers` — customer + store/person join, single-row fan-out via `OUTER APPLY`
  for e-mail, phone (uses `PersonPhone.PhoneNumber`), and primary address.
- `GetProducts` — product joined to subcategory/category names.
- `GetOrders` — exactly two round trips: order headers, then details for the same
  `TOP (@MaxRows)` header set; details grouped client-side per order. No N+1.
- `GetProductInventory` — inventory joined to product + location names.

## Lifecycle

- Windows Service (`ServiceBase`), installed with `sc create`; EventLog logging
  (source `SyncAgent`, log `Application`).
- `--console` / interactive run for development: same DI graph, Console logger, Ctrl+C
  stops gracefully.
- `OnStart` starts the worker task and returns immediately (stays under the SCM
  30 s window). `OnStop`/`OnShutdown` cancel the worker and wait up to 10 s.

## Verification

- `dotnet build` — 0 warnings, 0 errors (Release).
- Fast tests: `dotnet test --filter Category!=Integration` — 18/18 green.
- Integration tests (real LocalDB): `$env:ADVENTUREWORKS_CONN=...; dotnet test`.
- Manual E2E: run SyncPlatform, run agent with `--console`, click all four task
  buttons, confirm `accepted:true` and `recordCount > 0`.

## Known limitations

- LocalDB is per-user: the agent running as a Windows Service under LocalSystem
  cannot see `(localdb)\MSSQLLocalDB`. For service deployment point the connection
  string at a server instance, or run the service as the LocalDB-owning user.
- `modifiedSince` dedupe relies on the platform not redelivering; see idempotency note.
- Integration tests require a local AdventureWorks2025 restore (not included in repo).
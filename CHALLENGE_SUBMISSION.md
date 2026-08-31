# Challenge Submission

## Candidate

- **Name:** Pasqual Ferrari Navarro
- **Date:** 2026-08-30

---

## How to Run

**Prerequisites**

- .NET 8 SDK
- SQL Server 2025 with the AdventureWorks2025 database restored. This was run via Docker:
  ```bash
  docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<password>" \
    -p 1433:1433 --name sql2025 --hostname sql2025 -d \
    mcr.microsoft.com/mssql/server:2025-latest
  ```
  then restored from `AdventureWorks2025.bak` per the repo root [README](README.md#database-setup--adventureworks-oltp).

**Steps**

1. **Set the database connection string** via user-secrets (never committed - `appsettings.json` only holds an empty placeholder):
   ```bash
   cd candidate/src/SyncAgent.Worker
   dotnet user-secrets set "ConnectionStrings:AdventureWorks" "Server=localhost,1433;Database=AdventureWorks2025;User Id=sa;Password=<your-password>;TrustServerCertificate=True;"
   ```
   Or in Visual Studio: right-click **SyncAgent.Worker** → **Manage User Secrets**, and paste the same value in under `ConnectionStrings:AdventureWorks`.

2. **Start the test platform** (per the repo root README):
   ```bash
   cd src/SyncPlatform
   dotnet run --project SyncPlatform
   ```
   A WPF window opens, serving the API on `http://localhost:5100`.

3. **Run the sync agent**:
   ```bash
   cd candidate/src/SyncAgent.Worker
   dotnet run
   ```
   Or open `candidate/SyncAgent.sln` in Visual Studio and hit Start. It connects to AdventureWorks immediately (logging the server version) and begins polling every 5 seconds.

4. **Trigger a task**: click one of the enqueue buttons in the SyncPlatform window (Get Customers / Products / Orders / Inventory). Within ~5 seconds, the agent's console logs the task being received, executed, and a "completed" result posted back - visible in both the agent's own logs and SyncPlatform's log viewer.

5. **Run the automated tests**:
   ```bash
   dotnet test candidate/SyncAgent.sln
   ```

---

## Architecture Decisions

**Worker Service, not a classic Windows Service or plain console app.** Built on `Microsoft.Extensions.Hosting`'s `BackgroundService` + Generic Host, with `AddWindowsService()` wired in. This gets DI/config/logging for free, and the same binary behaves as a plain console app under `dotnet run` while being installable as a real Windows Service (auto-start at boot, no login required, proper SCM stop handling) via one line - no separate build config, no hand-rolled `ServiceBase` boilerplate. The core polling/dispatch logic lives in plain classes, testable in isolation from the OS service plumbing.

**Project layout** (`candidate/src/SyncAgent.Worker/`):
- `Program.cs` - composition root: DI registrations, startup DB connectivity check.
- `Worker.cs` - the polling loop and per-task orchestration (`ExecuteAsync` / `ProcessTaskAsync`).
- `Data/` - `ISqlConnectionFactory`, a thin factory over `Microsoft.Data.SqlClient` so task handlers depend on an interface, not a concrete connection.
- `Platform/` - the SyncPlatform HTTP contract: `SyncTask`, `SyncTaskType`, `TaskParameters`, `SyncResult`, `ISyncPlatformClient`/`SyncPlatformClient`.
- `Tasks/` - the Strategy pattern dispatch: `ISyncTaskHandler` per task type, `ISyncTaskDispatcher`/`SyncTaskDispatcher`, and `Tasks/Models/` for the four result DTOs.
- `candidate/tests/SyncAgent.Worker.Tests/` - the unit test suite.

**Strategy pattern for task dispatch.** Each of the four task types is its own `ISyncTaskHandler`, declaring its own `SyncTaskType`. `SyncTaskDispatcher` builds a lookup dictionary from whatever handlers are registered in DI and routes by type. Adding a fifth task type means one new handler class plus one `AddSingleton` line in `Program.cs` - nothing else in the pipeline changes.

**Dapper over Entity Framework Core.** This workload is entirely read-only against a fixed schema the agent doesn't own or migrate - EF's change-tracking and `DbContext` lifecycle buy nothing here. More concretely, `GetCustomersTaskHandler` needs `OUTER APPLY (SELECT TOP 1 ...)` for email/phone/address to avoid row fan-out (a person can have multiple rows in each table), and `GetOrdersTaskHandler` needs a flatten-then-regroup for the nested `orderDetails` shape - both are straightforward hand-written SQL, but fighting EF's LINQ translation to produce the same query shapes reliably would cost more than it saves. Every query was hand-verified against the live database via `sqlcmd` before any C# was written.

**DI lifetimes: Singleton, not Scoped, for the connection factory, task handlers, and dispatcher.** They hold no per-task state, so there's no correctness reason to scope them - and `Worker` (which consumes them) is itself always a Singleton, since the Generic Host registers every `IHostedService` that way. A Scoped registration here would fail DI validation at startup regardless of intent.

**.NET 8 target and a classic `.sln`, not the SDK's newer defaults.** The repo's README states ".NET 8 SDK or later" as the prerequisite; targeting `net8.0` explicitly (rather than the `net10.0` the local SDK defaults to) keeps the build reproducible on whatever SDK the evaluator has. Likewise, a classic `.sln` was used instead of the newer `.slnx` format for broader tooling compatibility.

**Configuration-driven, not hard-coded.** The database connection string, SyncPlatform base URL/API key, and polling interval are all read from configuration (`appsettings.json` + `IOptions<T>`), not hard-coded - see Security Measures for which of these is a secret and which isn't.

---

## Security Measures

- **The database connection string (with the `sa` credential) is never committed.** `appsettings.json` holds an empty placeholder; the real value lives only in `dotnet user-secrets` (or, in a real deployment, environment-specific secret storage), entirely outside the repo.
- **The SyncPlatform API key is committed in plain config, deliberately.** It's the shared, publicly-documented test key from `docs/api-contract.md`, not a personal credential - treating it as a secret to hide would add friction without adding real security.
- **All SQL parameters are bound via Dapper (`@ModifiedSince`), never string-concatenated** - the four handlers are not susceptible to SQL injection by construction, not by convention.
- **Explicit 401 handling** on both `GetNextTaskAsync` and `PostResultAsync` throws a clear "check your API key configuration" message instead of a generic HTTP exception - fails informatively, not silently.
- **Explicit 400 handling** on `PostResultAsync` surfaces the platform's own validation error text in the exception, aiding diagnosis without exposing anything sensitive.
- **A single task failure doesn't take down the agent or get silently dropped.** A query exception (bad data, unsupported task type, transient DB issue) is caught, reported back to the platform as a `"failed"` result with the error message, and polling continues - one bad task can't kill the always-on process, and the platform is told what happened rather than nothing.
- **Fail-fast startup check.** The agent verifies the AdventureWorks connection at startup and logs the server version - a bad connection string or unreachable SQL Server surfaces immediately, not as a confusing failure on the first poll.
- **Input validation on configuration**, not just task data: the connection-string check treats both `null` and an empty/whitespace string as "missing" (a real bug we hit and fixed - `GetConnectionString` returns `""`, not `null`, for the unset placeholder).

**Not implemented, worth calling out honestly:** no rate limiting or backoff on the agent's own outbound requests, and no authentication/authorization on the agent itself (it's a single-tenant local process, not a service other systems call into) - see Known Limitations.

---

## Testing Strategy

**What's covered by the 15 automated unit tests** (`candidate/tests/SyncAgent.Worker.Tests`), and why - these are the pieces with real branching logic, not thin pass-through wrappers:

- `SyncTaskDispatcherTests` - routes to the handler matching a task's type; throws for an unregistered type.
- `SqlConnectionFactoryTests` - throws on `null`/empty/whitespace connection string, succeeds when one is provided. This is a direct regression test for a real bug hit during development (see Known Limitations / commit history).
- `SyncPlatformClientTests` - the actual wire contract: 204 → `null`, 200 → correct deserialization, 401 → clear exception on both endpoints, 400 → the platform's own error message surfaced, 200 on POST doesn't throw.
- `WorkerTests` - `ProcessTaskAsync` posts a `"completed"` result with data and record count on success, a `"failed"` result with the exception message when the dispatcher throws, and lets cancellation propagate rather than reporting it as a failed task.

**Manual verification performed alongside the automated tests** (documented in the commit history, not just asserted here): every one of the four SQL queries was hand-run via `sqlcmd` against the live AdventureWorks2025 database and checked field-for-field against the documented sample payloads before any C# was written. The full poll → dispatch → post round trip was then verified against a live SyncPlatform instance and the real database via a temporary CLI hook (added, exercised, and removed before each relevant commit) - confirming all four task types execute and their results are accepted (HTTP 200), plus a synthetic failed-result payload is also accepted.

**What I'd test with more time:**
- The four task handlers' actual SQL, as automated tests - would need a seeded test database or Testcontainers; verified manually here instead (see above) to keep test infrastructure proportionate to the time available.
- `Worker.ExecuteAsync`'s outer polling loop (timing, cancellation-during-delay, retry-after-failure behavior) - currently only the inner `ProcessTaskAsync` logic is unit tested; the loop itself was exercised manually via `dotnet run`.
- A genuine GUI-driven end-to-end test (clicking the SyncPlatform buttons) - not independently confirmed in this session; there's no HTTP endpoint to enqueue a task programmatically, so this would need UI automation tooling to script.

---

## Known Limitations

- **No automated tests for the four query handlers' SQL** - manual verification only (see Testing Strategy). This is the most significant testing gap.
- **No retry/backoff policy** on transient failures talking to the platform - a failed poll or post just waits for the next fixed interval. A resilience library (e.g. Polly) would be a natural addition for exponential backoff on repeated failures.
- **Console-only logging.** Once actually installed as a Windows Service, there's no console to view at all - file-based logging (e.g. Serilog, writing to both console and a rolling file) was discussed but not implemented in this pass.
- **Fixed polling cadence.** The interval is configurable but static - the agent doesn't slow down when the platform is unreachable or speed up under load.
- **Windows Service installation was not empirically tested.** `AddWindowsService()` was verified to compile and run correctly as a console app; actually installing it via `sc create`/`services.msc` and confirming SCM start/stop behavior was deliberately out of scope for the time available, in favor of spending that time on query correctness and the dispatch pipeline.
- **No load/volume testing.** Queries were verified for correctness against known sample records, not benchmarked against the full dataset (e.g. `GetCustomers` returns ~19k rows unfiltered) for latency or payload size.

---

## AI Tools Used

This entire challenge was built in close collaboration with **Claude Code** (Anthropic's CLI agent), used as an active pair-programming partner throughout - not just for autocomplete or boilerplate. Specifically:

- **Architecture discussion and decisions**: talked through Worker Service vs. classic `ServiceBase` vs. plain console app, Dapper vs. EF Core, and DI lifetime choices (Singleton vs. Scoped, and why) before committing to each direction - I asked "why" at each major decision point and required a reasoned answer, not just a default.
- **Implementation**: Claude wrote the actual C# (DTOs, task handlers, the dispatcher, the HTTP client, `Worker`'s orchestration logic) and the SQL queries, based on schemas it explored and verified directly against the live database.
- **Verification, not just generation**: before writing any handler code, Claude hand-ran each SQL query via `sqlcmd` against the real AdventureWorks2025 database and cross-checked results against the documented sample payloads. After writing the dispatch/posting logic, it exercised the full round trip against a live SyncPlatform instance via temporary debug hooks, which were removed before the corresponding commit.
- **Debugging a real environment issue**: diagnosed why a `dotnet user-secrets` value set from the CLI tool's shell wasn't visible to Visual Studio (a filesystem isolation boundary between the two environments), and guided the fix (setting it via VS's "Manage User Secrets" instead).
- **Review loop**: I reviewed every commit's diff before approving it and asked for changes several times - correcting an over-eager target framework choice, trimming overly verbose code comments in favor of explaining reasoning in commit messages instead, tuning logging levels, and catching a connection string with a real password that had been placed directly into a committed config file (reverted before it was ever committed).
- **Commit messages**: written by Claude to explain the reasoning behind each change, per my direction; I reviewed and approved every commit before it was made.

---

## Time Spent

Approximate breakdown by activity (this was done across more than one sitting, so treat this as a rough shape rather than precise hours):

- Environment setup (Docker SQL Server 2025, AdventureWorks2025 restore, SSMS/tooling decisions): ~30 min
- Architecture discussion and project scaffolding (step 0): ~20 min
- Database connectivity + config/secrets handling, including debugging the user-secrets/VS issue (step 1): ~30 min
- Platform polling client (step 2): ~20 min
- Schema exploration, query verification, and the four task handlers (step 3): ~30 min
- Result posting back to the platform (step 4): ~15 min
- Logging cleanup: ~15 min
- Unit tests: ~15 min
- This document: ~15 min

---

## Feedback

- The setup instructions in the root README assume a native SQL Server install (`C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\...`); a Docker-based path (which is what was actually used here) works just as well but isn't mentioned as an option.
- There's no HTTP endpoint to enqueue a task programmatically - only the SyncPlatform GUI's buttons do it. That makes fully scripted end-to-end testing (without a person clicking through the UI) impossible; a `POST /api/sync/enqueue`-style test-only endpoint would help candidates verify their agent without manual interaction.
- Otherwise, the challenge was well-scoped: the sample payloads made the target shape unambiguous, and the four task types offered a good range of query complexity (from a straightforward join to the header/detail flattening in `GetOrders`).

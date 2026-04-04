# Challenge Submission

## Candidate

- **Name:** Franco Emilio Marino Berra
- **Date:** 20/03/2026

---

## How to Run

1. Make sure you have SQL Server with the AdventureWorks2025 database restored.

2. Create `candidate/SyncAgent.Worker/appsettings.Development.json` with your connection string:
   ```json
   {
     "SyncAgent": {
       "ConnectionString": "Server=YOUR_SERVER;Database=AdventureWorks2025;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

3. Start the SyncPlatform:
   ```bash
   cd src/SyncPlatform
   dotnet run --project SyncPlatform
   ```

4. In a separate terminal, start the sync agent:
   ```bash
   cd candidate/SyncAgent.Worker
   dotnet run
   ```

5. Click any button in the SyncPlatform window — the agent picks it up within 10 seconds, runs the query, and posts the result back.

6. To run the tests:
   ```bash
   cd candidate
   dotnet test
   ```

---

## Architecture Decisions

### Why a Worker Service

The challenge describes an "always-on application" that polls periodically. I considered a plain console app, but a **Worker Service** (`Microsoft.Extensions.Hosting`) gives you dependency injection, configuration, logging, and graceful shutdown out of the box — things I'd have to wire manually otherwise. It can also run as a Windows Service with a one-line change (`UseWindowsService()`), which makes sense for a real on-premise sync agent. It was the most natural fit for a long-running background process in .NET.

### Project structure (Clean Architecture)

I split the solution into three projects:

- **SyncAgent.Core** — models, DTOs, and interfaces. Has zero external dependencies.
- **SyncAgent.Infrastructure** — the actual implementations: SQL queries with Dapper and the HTTP client that talks to the platform.
- **SyncAgent.Worker** — the entry point. Configures DI and runs the polling loop. It doesn't know anything about SQL or HTTP directly.

Dependencies flow one way: **Worker → Infrastructure → Core**. This keeps things clean and testable — you can mock any layer without pulling in the others.

### Why Dapper instead of EF Core

Honestly, I'm more comfortable with EF Core and LINQ — they give me more confidence in day-to-day development with type safety, migrations, and change tracking. But for this specific scenario, I decided to step out of my comfort zone. We're reading data from a database we don't own, the challenge explicitly asks to "explore the database and build queries", and mapping 15+ AdventureWorks tables in a DbContext would eat into the time budget without adding real value for a read-only agent. With Dapper, each handler is its own repository: one class, one query, one responsibility. It was the right trade-off for this context.

### Strategy Pattern for task handlers

Each task type has its own handler class implementing `ISyncTaskHandler`. Adding a new task type means creating a new class and registering it in `Program.cs` — nothing else needs to change. The Worker resolves handlers by `TaskType` from a dictionary, which also acts as a whitelist: unknown task types are rejected before any code runs.

---

## Security Measures

- **Parameterized queries** — Dapper uses SQL parameters natively (`@ModifiedSince`), which prevents SQL injection. No string concatenation anywhere near SQL. Worth noting: `modifiedSince` is the only external input that reaches any SQL query — `taskType` is validated against a whitelist before execution and `taskId` is only used in the POST response, never in SQL. So the attack surface is minimal by design.
- **API key in a DelegatingHandler** — the key is injected automatically into every HTTP request via `ApiKeyDelegatingHandler`, so it's never hardcoded in the client logic. The value itself lives in configuration, not in code.
- **Task type whitelist** — the Worker only executes task types that have a registered handler. If the platform sends something unexpected like `DropDatabase`, it gets rejected with a failure response.
- **Connection string out of source control** — `appsettings.Development.json` is in `.gitignore`. The committed `appsettings.json` only has a placeholder.

---

## Testing Strategy

I focused tests on the **Worker polling logic** since that's the core orchestration — the part where things are most likely to go wrong at runtime:

- No task available → agent does nothing (doesn't post empty results)
- Known task received → handler runs and result is posted as success
- Unknown task type → failure is reported to the platform
- Handler throws an exception → failure is reported and the agent keeps running

I also added tests that verify each handler declares the correct `TaskType` string. This sounds trivial, but a typo there means the platform sends tasks that silently never execute.

**With more time I would add:**
- Integration tests running actual SQL against a test database
- Tests for `PlatformClient` with a mock HTTP server
- Edge case tests for the `SyncResult.Success/Failure` factory methods

---

## Known Limitations

- **No retry logic** — if a request to the platform fails, the agent just logs the error and moves on. In production I'd add exponential backoff with Polly.
- **No incremental sync tracking** — the agent doesn't remember what it already synced. Each task is independent. A real agent would probably track watermarks.
- **Single-threaded polling** — the agent processes one task per polling cycle. If the queue has many tasks, it takes multiple cycles. Parallelism would help but adds complexity.
- **No health checks** — there's no endpoint to monitor if the agent is alive. A production service would expose one.
- **No input format validation** — if the platform sends a malformed `modifiedSince` value, Dapper throws an exception that gets caught and reported as a failure. It works, but the error message isn't user-friendly. With more time I'd validate the format upfront and return a descriptive error.

---

## AI Tools Used

- **Claude Code (CLI)** — used throughout the entire process as my primary development partner. I used it to:
  - Plan the architecture and project structure before writing any code
  - Scaffold the .NET solution and projects via CLI commands
  - Generate the models, DTOs, interfaces, handlers, and Worker logic
  - Write and debug the SQL queries (we verified the AdventureWorks FKs together by querying `sys.foreign_keys`)
  - Fix build errors (missing NuGet packages, InternalsVisibleTo syntax)
  - Fix a runtime bug where GetOrders exceeded SQL Server's 2100 parameter limit
  - Write the unit tests
  - Fill in this submission document

I was driving the decisions and reviewing every piece of code before accepting it. Claude Code proposed, I validated and asked questions when something didn't feel right (like whether the SQL lived in the right place or whether we needed Docker).

---

## Time Spent

- **~30 min** — Planning: reading the challenge, understanding AdventureWorks, deciding on architecture
- **~20 min** — Scaffolding: creating the solution, projects, and adding NuGet packages
- **~50 min** — Implementation: Core models/DTOs, Infrastructure handlers and HTTP client, Worker wiring
- **~25 min** — Debugging: fixing build errors, fixing the 2100 parameter limit bug
- **~15 min** — Tests: writing and running the 8 unit tests
- **~25 min** — Review, documentation, and cleanup

**Total: ~2 hours 45 minutes**

PD: I got a migraine right before the interview today, but I still wanted to get this done because I'm genuinely interested in the role and the company. Took me a bit longer reviewing everything, but I preferred being thorough.

---

## Feedback

Clear and well-structured challenge. The SyncPlatform WPF app with the log viewer was really helpful for testing — being able to see the requests and responses in real time made debugging easy. The sample payloads were the right amount of guidance: enough to know what's expected, but you still have to figure out the SQL yourself.

One small thing: the README says to run `dotnet run --project SyncPlatform` from `src/SyncPlatform`, but since you're already in that folder it could just be `dotnet run`. Not a big deal, just noticed it.

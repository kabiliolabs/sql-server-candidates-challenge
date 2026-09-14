# SyncAgent

A .NET 8 Worker Service that continuously polls the `SyncPlatform` HTTP server for pending sync tasks, executes the matching read-only query against the AdventureWorks database via EF Core, and reports the result back to the server.

## Project Structure

```
candidate/
  SyncAgent/                     Worker Service (the agent itself)
	Api/                         Typed HttpClient for the SyncPlatform server
	Configuration/                SyncAgentOptions + startup validation
	Contracts/                    Wire DTOs (SyncTaskDto, SyncResultDto, *Dto)
	Data/                         AdventureWorksDbContext, entities, repository
	Handlers/                     ITaskHandler strategy + one handler per task type + dispatcher
	Program.cs                    Composition root (DI registration)
	Worker.cs                     BackgroundService polling loop
  SyncAgent.Tests/                xUnit test project (mirrors the folder structure above)
  docs/
	query-notes.md                Validated AdventureWorks query design + ER diagrams
	development-plan.md           Step-by-step commit plan followed during implementation
```

## How to Run

1. **Prerequisites**
   - .NET 8 SDK
   - SQL Server instance with the `AdventureWorks2025` (or compatible) sample database restored
   - The `SyncPlatform` WPF simulator (`src/SyncPlatform/SyncPlatform.sln`) running locally, listening on `http://localhost:5100`

2. **Configure secrets** (do not commit real connection strings/API keys):

   ```powershell
   cd candidate/SyncAgent
   dotnet user-secrets set "SyncAgent:ApiKey" "<your-api-key>"
   dotnet user-secrets set "SyncAgent:ConnectionString" "<your-connection-string>"
   cd ..
   ```

   `appsettings.json` ships with placeholder (empty) values for `ApiKey` and `ConnectionString` intentionally, plus a non-secret default for `ApiBaseUrl` and `PollIntervalSeconds`.

3. **Run the agent** (from `candidate/`):

   ```powershell
   dotnet run --project SyncAgent
   ```

   On startup, `SyncAgentOptions` is validated (`ValidateOnStart()`); a missing/invalid API base URL, API key, connection string, or non-positive poll interval fails fast with a clear error instead of surfacing as a confusing runtime failure on the first poll.

4. **Trigger tasks**: use the SyncPlatform WPF simulator's UI to enqueue one of the four task types (`GetCustomers`, `GetProducts`, `GetProductInventory`, `GetOrders`). The agent will pick it up on its next poll cycle (default every 5 seconds), execute the corresponding EF Core query, and POST the result back.

## Running Tests

```powershell
cd candidate
dotnet test SyncAgent.slnx
```

All 28 unit tests should pass. Tests use xUnit, Moq for mocking, and EF Core's InMemory provider for repository tests (no live database required to run the test suite).

## Architecture Overview

- **Strategy pattern for task types**: `ITaskHandler` is implemented once per task type (`GetCustomersTaskHandler`, etc.) and resolved at runtime by `TaskHandlerDispatcher` based on `taskType`. Adding a new sync task type only requires a new handler class and one DI registration line in `Program.cs` — no changes to the worker, dispatcher, or API client.
- **EF Core, read-only**: `AdventureWorksDbContext` maps only the ~16 tables the four queries actually need (not the full AdventureWorks schema). All queries use `AsNoTracking()` and projection-based LINQ (no raw SQL) to stay efficient and safe; `GetOrdersAsync` uses `AsSplitQuery()` to avoid row fan-out from the one-to-many order-detail join.
- **Scoped lifetimes in a long-running host**: `Worker : BackgroundService` is a singleton, but it resolves `ISyncPlatformApiClient`, `TaskHandlerDispatcher`, the repository, and `AdventureWorksDbContext` from a fresh `IServiceScopeFactory.CreateScope()` on every polling cycle, so scoped/EF Core state never leaks across cycles.
- **Consistent result contract**: `TaskHandlerBase` guarantees every result — success or failure — echoes back the original `TaskId`/`TaskType`, matching the SyncPlatform API contract exactly (including the `error-result.json` failed-task shape).

See `candidate/docs/query-notes.md` for the validated per-task-type ER diagrams and query design, and the repository root `CHALLENGE_SUBMISSION.md` for the full write-up (architecture decisions, security measures, testing strategy, known limitations).

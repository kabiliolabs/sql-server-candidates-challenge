# Challenge Submission

## Candidate

- **Name:** Adrien Couturier
- **Date:** 2026-06-20

---

## How to Run

1. **Connection to AdventureWorks database** Update the connection string in `cappsettings.json` (`ConnectionStrings:Sqlserver`) to match your instance and credentials.
2. **Run the sync agent**:
   ```bash
   cd candidate/SyncTaskWorkerService/SyncTaskWorkerService
   dotnet run
   ```
3. In the SyncPlatform window, click any of the **SyncCustomers / SyncProducts / SyncOrders / SyncInventory** buttons to enqueue a task. The worker polls every `PollingTime` (5000 ms by default, configurable in `appsettings.json`), picks up the task, queries AdventureWorks, and posts the result back.

---

## Architecture Decisions

.NET Worker Service (`BackgroundService`) built with four main parts, each with a single, narrow responsibility:

- **`Worker`**: owns only the polling loop (ask for a task → dispatch it → post the result → wait). It contains no business logic.

- **`IPlatformHttpClient` / `PlatformHttpClient`** : owns all HTTP communication with the central platform (`GET /api/sync/next-task`, `POST /api/sync/result`), including the `X-Api-Key` header.

- **`SyncTaskDispatcher`**: owns task routing. It receives registered `ISyncTaskHandler` via `IEnumerable<ISyncTaskHandler>` (DI) and picks the one whose `SyncName` matches the `TaskType`.

- **`ISyncTaskHandler` implementations** (`GetCustomersHandler`, `GetOrdersHandler`, `GetProductsHandler`, `GetProductInventoryHandler`): each owns exactly one query for AdventureWorks DB.

This is a **Strategy pattern**: `ISyncTaskHandler` is the strategy interface, each handler is a concrete strategy, and `SyncTaskDispatcher` is the strategy selector. Handlers are registered as a collection in DI (`Program.cs`) rather than through a `switch` statement, so **adding a new task type never touches existing code**, just add a new handler class implementing `ISyncTaskHandler` and register it. 
This is the Open/Closed requirement.

A small `IDbConnection` abstraction wraps `SqlConnection` creation so handlers depend on an interface rather than `Microsoft.Data.SqlClient` directly, keeping data access swappable/mockable.

Full SOLID-by-SOLID rationale, including why each principle mattered for *this* problem (recurring task types, frequent SQL changes, need for testability) is written up in `candidate/READMEADRIEN.md`.

---

## Security Measures

- **API key authentication**: every request to the platform carries `X-Api-Key`, configured once on the shared `HttpClient` in `Program.cs` rather than per call, so it can't be forgotten on a new endpoint.

- **Parameterized SQL everywhere**: all four handlers pass filter values (`modifiedSince`) through Dapper's parameter binding (`new { ModifiedDate = modifiedSince }`), never string concatenation, eliminating SQL injection risk even though the queries are hand-written.

- **Externalized secrets**: API key and SQL connection string live in `appsettings.json`/configuration rather than hardcoded in source, and the project has a `UserSecretsId` so they can be moved to User Secrets / environment variables for any real deployment.

---

## Testing Strategy

Given the 2-hour budget, I prioritized getting the end-to-end flow working over writing an automated test suite, **no test project was added**, which I consider the single biggest gap in this submission

---

## Known Limitations

- **No automated tests**: biggest trade-off made for time; see Testing Strategy above.

- **`GetOrdersHandler` I used AI help to shape the query result and build the group-by logic in code. With more time I would introduce a DTO to make this part clearer.

- **No retry** on HTTP failures, if `GetTaskAsync` or `PostResultAsync` fails, the loop just waits for the next `PollingTime` tick.

- **No structured exception handling** if a handler throws, it crashes the worker instead of being reported back..

- **Secrets in `appsettings.json`** rather than User Secrets/environment variables, fine for this exercise but not in production.

---

## AI Tools Used

I used Claude Code throughout this challenge:

- To explore the AdventureWorks schema relationships needed to write the `GetCustomers` and `GetOrders` queries.

- To help me decide on the Strategy pattern and get started.

- I used chat with claude to get help with code syntax.

- to reformulate the challenge submission document too.

---

## Time Spent

- ~40 min: exploring AdventureWorks schema and the API contract / sample payloads
- ~40 min: choosing the right pattern and architecture for the worker service
- ~60 min: implementing the Worker Service, DI wiring, `ISyncTaskHandler` strategy interface, dispatcher, and the four handlers
- ~20 min: end-to-end testing against the SyncPlatform test app
- ~20 min: documentation

**~3 hours total**. I went over the 2-hour suggestion, mostly on schema exploration and architecture decision.

---

## Feedback

The challenge was reasonable for 2 hours, but the main friction was exploring the AdventureWorks schema (several joins needed per task type). I spent a lot of time thinking through the architecture, which left no room for automated tests (unit, integration).
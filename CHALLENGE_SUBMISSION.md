# Challenge Submission

## Candidate

- **Name:** par-sw
- **Date:** August 16, 2026

---

## How to Run

### Prerequisites

- .NET 10 SDK
- SQL Server
- AdventureWorks2025 database restored locally
- The provided SyncPlatform application

### 1. Start the provided SyncPlatform

From the repository root:

```bash
cd src/SyncPlatform
dotnet run --project SyncPlatform
```

The platform API runs at:

```text
http://localhost:5100
```

### 2. Configure the Sync Agent

The development configuration is located at:

```text
candidate/src/SyncAgent/appsettings.json
```

The provided local configuration assumes:

- SQL Server: `localhost`
- Database: `AdventureWorks2025`
- Windows Authentication
- SyncPlatform: `http://localhost:5100`
- API key: `candidate-test-key-2026` (public test key supplied with the challenge)
- Polling interval: `PollingIntervalMSec` = 1000 ms (1 second, `appsettings.json`) / 10000 ms (10 seconds, `appsettings.Development.json`)
- HTTP request timeout: `RequestTimeoutMsec` = 30000 ms (30 seconds)

The SQL Server connection string and SyncPlatform settings can be overridden through the standard .NET configuration providers/environment variables.

### 3. Build the Sync Agent

From the `candidate` directory:

```bash
dotnet build SyncAgent.slnx
```

### 4. Run the Sync Agent

From the `candidate` directory:

```bash
dotnet run --project src/SyncAgent/SyncAgent.csproj
```

Once both applications are running, use the buttons in SyncPlatform to enqueue any of the supported tasks:

- `GetCustomers`
- `GetProducts`
- `GetOrders`
- `GetProductInventory`

The Sync Agent polls the platform, executes the corresponding AdventureWorks query, and submits the result back to SyncPlatform.

### 5. Run Automated Tests

From the `candidate` directory:

```bash
dotnet test SyncAgent.slnx
```

---

## Architecture Decisions

I implemented the solution as a **.NET 10 Worker Service** because the sync agent is an always-on background process that periodically polls a central platform and does not require a user interface.

**.NET 10** was chosen because it is the latest LTS release: being on the newest runtime lets the solution take advantage of the performance improvements and platform features that come with using the latest technology.

The solution is intentionally split into two small projects by responsibility:

- **`AdventureWorks.Ef`** — the AdventureWorks entity model and `AdventureWorksDbContext`, generated **database-first** with `dotnet ef dbcontext scaffold`: the database already existed, so the code was generated from it. This is the perfect case for scaffolding, and the model is kept as pure scaffold output. The project lives separately because it could be reused by other applications that work against the same schema. The scaffold command used:

  ```bash
  dotnet ef dbcontext scaffold "Server=localhost;Database=AdventureWorks2025;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer --schema Production --table Person.BusinessEntity --table Person.BusinessEntityAddress --table Person.Person --table Person.EmailAddress --table Person.PersonPhone --table Person.Address --table Person.StateProvince --table Person.CountryRegion --table Sales.Customer --table Sales.SalesOrderHeader --table Sales.SalesOrderDetail --table Sales.Store --context AdventureWorksDbContext --namespace AdventureWorks.Ef.Models --context-namespace AdventureWorks.Ef.Data --output-dir Models --context-dir Data --no-onconfiguring --no-pluralize --force --project src/AdventureWorks.Ef --startup-project src/AdventureWorks.Ef
  ```

  Relationships the database expresses through composite foreign keys (e.g., `SalesOrderDetail → Product` via `Sales.SpecialOfferProduct`) are resolved with LINQ joins in the queries rather than hand-editing the generated model.
- **`SyncAgent`** — the worker, HTTP client, task dispatcher, repository, DTOs, and configuration.

### Main components

- **`SyncWorker`**
  - Controls the polling lifecycle.
  - Retrieves tasks from SyncPlatform.
  - Validates incoming tasks (`taskId`, `taskType`, `modifiedSince`).
  - Coordinates execution and result submission.
  - Converts processing exceptions into `failed` task results instead of terminating the service, and logs-but-swallows submission failures so a failed POST does not stop the polling loop.

- **`ISyncPlatformClient` / `SyncPlatformClient`**
  - Encapsulates HTTP communication with the provided SyncPlatform API.
  - Handles both `GET /api/sync/next-task` and `POST /api/sync/result`.
  - Treats `204 No Content` as an empty task queue; non-success statuses surface as `HttpRequestException`.

- **`ISyncTaskDispatcher` / `SyncTaskDispatcher`**
  - Encapsulates the **strategy pattern** in a single dependency: it maps each incoming `taskType` to its corresponding repository query through a switch expression. A comment in the code documents when this should become an explicit strategy registry (if the set of task types grows or becomes dynamic).
  - Requires `modifiedSince` and rejects unsupported task types.
  - Matching is case-sensitive by design: task types are a fixed machine-to-machine contract, not free-form user input.

- **`IAdventureWorksRepository` / `AdventureWorksRepository`**
  - Encapsulates SQL Server access and all AdventureWorks-specific queries using EF Core LINQ with `AsNoTracking()` and server-side projections, so each task produces a single SQL statement.
  - For `GetOrders`, the schema lacks a direct FK between `SalesOrderDetail` and `Product` (the relationship goes through the composite `Sales.SpecialOfferProduct`), which was missed when scaffolding. This forced an explicit LINQ `Join` to `Product` plus a client-side grouping to rebuild the order→details hierarchy in one round trip — all without touching the generated model.

- **DTOs as `sealed record`s**
  - All payload DTOs are immutable records with `init` accessors, giving value-based equality (useful for test assertions) at no serialization cost — `System.Text.Json` handles them identically to classes.

- **Serilog**
  - Logging is persisted to disk with configuration: console and file sinks configured entirely through `appsettings.json`, with daily rolling, 14 retained files, a 10 MB per-file cap, and flush-on-interval, so runtime diagnostics survive restarts.

- **Dependency Injection**
  - Interfaces are registered through the .NET dependency injection container.
  - The Worker depends on abstractions rather than concrete HTTP/database implementations.

### Extensibility

Adding a new synchronization task requires:

1. Adding the required DTO and query to the repository.
2. Adding a case to the dispatcher switch.

The Worker itself does not need task-specific branching logic.

I intentionally avoided adding additional architectural layers that would provide little value for the size of this exercise. The objective was to keep the solution extensible and testable while avoiding unnecessary complexity.

---

## Security Measures

The following measures were implemented:

- **Parameterized queries**
  - Filters such as `modifiedSince` are bound by EF Core as SQL parameters rather than concatenated into query text.
  - This avoids introducing SQL injection through task parameters.

- **API authentication**
  - All communication with SyncPlatform includes the required `X-Api-Key` header, read from configuration.

- **HTTP timeout**
  - The SyncPlatform HTTP client has a configurable timeout (`RequestTimeoutMsec`) so calls cannot wait indefinitely.

- **Startup configuration validation**
  - `BaseUrl`, `ApiKey`, `PollingIntervalMSec`, and `RequestTimeoutMsec` are validated with `Options.ValidateOnStart()` before the service starts.

- **Input validation**
  - Incoming tasks are checked for required `taskId`, `taskType`, and `modifiedSince` values before execution.

- **Explicit task dispatching**
  - Only the four known task types can be executed; anything else is rejected.

- **Integrated SQL Server authentication**
  - The local challenge configuration uses Windows Authentication rather than embedding a SQL username/password.

The API key stored in the development configuration is the public test key supplied with the challenge. In a production environment, credentials and API keys should be supplied through environment variables or a secrets-management solution rather than committed to source control, and `TrustServerCertificate=True` (appropriate for this local setup) should not be the production TLS strategy.

---

## Testing Strategy

I used both automated tests and end-to-end testing.

### Automated tests

The xUnit test project contains **13 tests**, written entirely with AI assistance:

**`SyncPlatformClientTests` (6)**
- `204 No Content` is treated as an empty task queue.
- A `200` response with a task payload deserializes correctly (including `modifiedSince`).
- `SubmitResultAsync` posts the serialized result to the correct endpoint.
- HTTP errors throw `HttpRequestException` for both `GetNextTaskAsync` and `SubmitResultAsync`.
- A `200` with an empty payload throws `InvalidOperationException`.

**`SyncTaskDispatcherTests` (7)**
- Dispatch to each of the four repository methods.
- Case-sensitive task-type matching.
- Unsupported task types are rejected.
- Missing `modifiedSince` is rejected.
- `modifiedSince` is forwarded to the repository unchanged.
- The `CancellationToken` is forwarded to the repository.

The suite can be executed with:

```bash
dotnet test SyncAgent.slnx
```

### End-to-end testing

I tested the complete workflow using the provided SyncPlatform application, local SQL Server, and AdventureWorks2025. For each of the four task types, the agent:

1. Retrieved the queued task from SyncPlatform.
2. Parsed and validated the task.
3. Queried AdventureWorks.
4. Generated the expected response structure.
5. Submitted the result to SyncPlatform.
6. Received a successful response with `status: completed`.

### Additional testing with more time

- Repository integration tests against SQL Server (the EF query-to-SQL equivalence was validated during development with a comparison harness against the original SQL, but not committed as automated tests).
- `SyncWorker` lifecycle tests (the assistant pointed out this would require injecting `TimeProvider` and exposing the per-task processing path; in my view, integration tests reviewed by another person are preferable to changing the production code just to enable unit tests).
- Retry/dead-letter behavior for failed result submissions.
- Cancellation/shutdown behavior.
- Larger-volume performance tests.

---

## Known Limitations

- **Fixed polling interval**
  - The agent polls on a fixed interval (1 second by default, 10 seconds in Development).
  - A production implementation could introduce exponential backoff when SyncPlatform is unavailable.

- **Sequential task processing**
  - Tasks are processed one at a time.
  - This keeps execution predictable, but controlled parallelism could be introduced if higher throughput were required.

- **No durable local task state**
  - Retrieved tasks are not persisted locally, and failed result submissions are logged and dropped.
  - A production implementation could use durable state, an outbox, or retry/dead-letter mechanisms.

- **Single-worker design**
  - A future design could split the work into two workers: one polls the API and enqueues tasks into an in-memory or disk-backed queue, and a second consumes them with N threads and submits the results. This would parallelize processing and, with a persistent queue, would guarantee no tasks are lost between reception, execution, and result submission. A persisted queue such as RabbitMQ could be used instead of disk.

- **Results are materialized in memory**
  - Query results are loaded before being submitted.
  - Very large datasets could benefit from pagination, batching, or streaming.

- **Development SQL certificate configuration**
  - `TrustServerCertificate=True` is appropriate for this local challenge setup but should not be the production TLS strategy.

- **Case-sensitive task types**
  - Task types must match exactly; this is a deliberate contract decision.

- **Configuration validation in `Program.cs`**
  - Validation currently lives inline in the composition root.
  - Moving it to a dedicated `IValidateOptions<T>` class would keep `Program.cs` cleaner.

- **Windows Service packaging**
  - The agent is currently run as a console process.
  - If the target deployment were a Windows service, the solution could be prepared with the Windows Service worker extensions (`AddWindowsService`) plus an installer (e.g., WiX or MSI) for automatic startup, lifecycle management, and logging integration.

---

## AI Tools Used

The implementation was done with **OpenCode** using free models, driven by the **Big Pickle** model on **OpenCode Zen** (free plan). It was completed over several days because of the daily token limits of the free plan; I do not have a personal subscription, and this model covered my needs for the challenge.

The assistant was used in a **guided programming** way: I started from an already-made codebase that followed my preferred structure and style, and the assistant followed those existing implementations rather than designing from scratch. For example, the first LINQ query against the database was written 100% by me; the remaining queries and their EF Core migration were then implemented through guided programming, with the assistant matching the patterns already established in the code.

The assistant was used to:

- Follow the existing Worker Service, HTTP client, repository, and DTO implementations, adapting them where the task required
- Implement the remaining repository queries and the migration to EF Core, following the pattern of the first query
- Write the automated tests (the testing was done fully with AI assistance)
- Review and finalize this documentation

All AI-assisted code and suggestions were reviewed, built, executed, and validated locally against the provided SyncPlatform application and AdventureWorks2025 database.

---

## Time Spent

Around **12 hours** in total, spread as follows:

- Understanding the challenge and its requirements.
- Setting up the environment, including updating my tooling to work with the SQL Server version and the received projects.
- Implementing the solution (worker, HTTP client, repository and EF Core queries, DTOs, dispatcher, and configuration).

I did not invest heavily in testing: the goal was to demonstrate the knowledge and techniques involved, not to make everything perfect. Making the solution bulletproof (broader test coverage, hardening, edge-case handling) would have required roughly **8 more hours**.

---

## Feedback

It has been an interesting challenge. A couple of observations:

- Understanding the database was perhaps too big a task to be able to deliver quick queries; the schema takes time to map before you can write them efficiently.
- You do not fully understand what the challenge asks until you open all the necessary dependencies and see everything together: the SQL, the API, the example JSON contracts, etc.
- Since the delivery format is a public PR, my strategy was simply to start from another candidate's solution, because it already had many things that were useful to me (project setup, DTOs, etc.). From there, as you can see, I built my own implementation according to my own criteria.

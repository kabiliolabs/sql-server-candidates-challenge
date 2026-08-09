# Challenge Submission

## Candidate

- **Name:** Esteban Peñaherrera
- **Date:** August 9, 2026

---

## How to Run

### Prerequisites

- .NET 8 SDK or later
- SQL Server
- AdventureWorks2025 database restored locally
- The provided SyncPlatform application

### 1. Start the provided SyncPlatform

From the repository root:

```bash
cd src/SyncPlatform
dotnet build
dotnet run --project SyncPlatform
```

The platform API runs at:

```text
http://localhost:5100
```

### 2. Configure the Sync Agent

The default development configuration is located at:

```text
candidate/src/SyncAgent/appsettings.json
```

The provided local configuration assumes:

- SQL Server: `localhost`
- Database: `AdventureWorks2025`
- Windows Authentication
- SyncPlatform: `http://localhost:5100`
- Polling interval: 5 seconds

The SQL Server connection string and SyncPlatform settings can be overridden through the standard .NET configuration providers/environment variables if required.

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

I implemented the solution as a **.NET 8 Worker Service** because the sync agent is an always-on background process that periodically polls a central platform and does not require a user interface.

The solution is intentionally small and divided by responsibility rather than introducing multiple application/domain/infrastructure projects for the scope of this challenge.

### Main components

- **Worker**
  - Controls the polling lifecycle.
  - Retrieves tasks from SyncPlatform.
  - Validates incoming tasks.
  - Coordinates execution and result submission.
  - Converts processing exceptions into failed task results instead of terminating the service.

- **`ISyncPlatformClient` / `SyncPlatformClient`**
  - Encapsulates HTTP communication with the provided SyncPlatform API.
  - Handles both `GET /api/sync/next-task` and `POST /api/sync/result`.
  - Treats `204 No Content` as an empty task queue.

- **`ISyncTaskDispatcher` / `SyncTaskDispatcher`**
  - Resolves each incoming `taskType` to its corresponding handler.
  - Uses case-insensitive matching.
  - Rejects unsupported task types.

- **`ISyncTaskHandler` implementations**
  - `GetCustomersHandler`
  - `GetProductsHandler`
  - `GetOrdersHandler`
  - `GetProductInventoryHandler`

  Each handler has one synchronization responsibility and delegates database access to the repository.

- **`IAdventureWorksRepository` / `AdventureWorksRepository`**
  - Encapsulates SQL Server access and all AdventureWorks-specific queries.
  - Keeps SQL concerns separate from task orchestration.

- **Dapper**
  - I chose Dapper because the challenge is SQL-oriented.
  - It keeps the SQL queries explicit while avoiding the additional complexity of a full ORM.

- **Dependency Injection**
  - Interfaces are registered through the .NET dependency injection container.
  - The Worker depends on abstractions rather than concrete HTTP/database implementations.

### Extensibility

Adding a new synchronization task primarily requires:

1. Adding the required DTO/query to the repository.
2. Implementing a new `ISyncTaskHandler`.
3. Registering the handler in dependency injection.

The Worker itself does not need task-specific branching logic.

I intentionally avoided adding additional architectural layers that would provide little value for the size of this exercise. The objective was to keep the solution extensible and testable while avoiding unnecessary complexity.

---

## Security Measures

The following measures were implemented:

- **Parameterized SQL queries**
  - Database filters such as `modifiedSince` are passed as Dapper parameters rather than concatenated into SQL strings.
  - This avoids introducing SQL injection through task parameters.

- **Explicit task dispatching**
  - Only registered task types can be executed.
  - Unsupported task types are rejected.

- **Input validation**
  - Incoming tasks are checked for required `taskId`, `taskType`, and `modifiedSince` values before execution.

- **API authentication**
  - All communication with SyncPlatform uses the required `X-Api-Key` header.

- **HTTP timeout**
  - The SyncPlatform HTTP client has a timeout configured so calls cannot wait indefinitely.

- **Startup configuration validation**
  - Base URL, API key, and polling interval are validated when the application starts.

- **Integrated SQL Server authentication**
  - The local challenge configuration uses Windows Authentication rather than embedding a SQL username/password.

The API key stored in the development configuration is the public test key supplied with the challenge. In a production environment, credentials and API keys should be supplied through environment variables or a secrets-management solution rather than committed to source control.

---

## Testing Strategy

I used both automated tests and end-to-end testing.

### Automated tests

The xUnit test project currently contains **5 tests** covering important behavior:

- Correct handler selection by `SyncTaskDispatcher`
- Case-insensitive task-type matching
- Rejection of unsupported task types
- Correct handling of `204 No Content` when no task is queued
- Correct deserialization of a task returned by SyncPlatform

The test suite can be executed with:

```bash
dotnet test SyncAgent.slnx
```

Current result:

```text
Total: 5
Succeeded: 5
Failed: 0
```

### End-to-end testing

I also tested the complete workflow using:

- The provided SyncPlatform application
- Local SQL Server
- AdventureWorks2025
- The Sync Agent

All four task types were individually tested successfully:

- `GetCustomers`
- `GetProducts`
- `GetOrders`
- `GetProductInventory`

For each task, the agent:

1. Retrieved the queued task from SyncPlatform.
2. Parsed and validated the task.
3. Queried AdventureWorks.
4. Generated the expected response structure.
5. Submitted the result to SyncPlatform.
6. Received a successful response with `status: completed`.

### Additional testing with more time

With more time I would add:

- Repository integration tests against a disposable/test SQL Server database
- Tests for failed task-result generation
- Tests for invalid/missing `modifiedSince`
- Tests for HTTP failures and timeouts
- Tests for cancellation/shutdown behavior
- Larger-volume performance tests

---

## Known Limitations

- **Fixed polling interval**
  - The agent currently polls every 5 seconds.
  - A production implementation could introduce exponential backoff when SyncPlatform is unavailable.

- **Sequential task processing**
  - Tasks are processed one at a time.
  - This keeps execution predictable, but controlled parallelism could be introduced if higher throughput were required.

- **No durable local task state**
  - Retrieved tasks are not persisted locally.
  - A production implementation could use durable state/outbox/retry mechanisms to improve resilience if the platform becomes unavailable after processing.

- **Results are materialized in memory**
  - Query results are loaded before being submitted.
  - Very large datasets could benefit from pagination, batching, or streaming.

- **Development SQL certificate configuration**
  - `TrustServerCertificate=True` is appropriate for this local challenge setup but should not be the production TLS strategy.

- **Simple retry strategy**
  - Communication failures are logged and the next polling cycle tries again.
  - A production solution would likely use an explicit retry/backoff policy and potentially distinguish transient from non-transient errors.

---

## AI Tools Used

I used **ChatGPT** as a development assistant during the challenge.

I used it to:

- Discuss the initial architecture and evaluate an appropriate .NET application type
- Review separation of responsibilities and SOLID-oriented design
- Assist with the implementation structure for the Worker Service, HTTP client, repository, DTOs, dispatcher, and task handlers
- Review AdventureWorks SQL relationships and Dapper query design
- Suggest automated test scenarios and test structure
- Help diagnose issues discovered during end-to-end testing
- Review the final documentation and submission structure

One concrete debugging example involved `GetOrders`.

The initial implementation retrieved the matching orders and then passed all selected order IDs to a second Dapper query using an `IN` clause. During end-to-end testing, the number of IDs exceeded SQL Server's maximum of 2,100 parameters.

After identifying the issue, I changed the implementation so SQL Server performs the relational filtering using the `modifiedSince` parameter instead of receiving thousands of order-ID parameters. This removed the parameter-limit problem and the four task types were then successfully validated end-to-end.

All AI-assisted code and suggestions were reviewed, built, executed, and validated locally against the provided SyncPlatform application and AdventureWorks2025 database.

---

## Time Spent

Approximately **4 hours**, including:

- Environment and AdventureWorks setup
- Reviewing the provided API contract and sample payloads
- Solution architecture and implementation
- AdventureWorks query development
- End-to-end testing and debugging
- Automated tests
- Documentation and final review

---

## Feedback

I found the challenge useful because it combines several concerns that commonly appear together in integration work:

- Understanding an unfamiliar database schema
- Building a long-running background process
- Communicating with an external API
- Designing extensible task processing
- Writing SQL for relational data extraction
- Handling realistic SQL Server constraints
- Testing the complete workflow

The provided SyncPlatform application and sample payloads made it straightforward to validate the implementation end-to-end while still leaving the database exploration and implementation decisions to the candidate.

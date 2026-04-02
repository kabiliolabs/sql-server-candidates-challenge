# SyncAgent - Challenge Submission

## Candidate

- **Name:** Aleix Pérez Vidal
- **Date:** 2026-03-23

---

## How to Run

### Prerequisites

- .NET 8 SDK
- SQL Server with `AdventureWorks2025` restored
- The provided `SyncPlatform` app running on `http://localhost:5100`

### Run the platform

From the repository root:

```bash
cd src/SyncPlatform
dotnet run
```

### Run the sync agent

From the repository root:

```bash
cd candidate/SyncAgent/SyncAgent.Worker
dotnet run
```

The agent polls the platform every 5 seconds by default.

### Configuration

The agent reads configuration from `candidate/SyncAgent/SyncAgent.Worker/appsettings.json`:

- `ConnectionStrings:AdventureWorks`: SQL Server connection string
- `Platform:BaseUrl`: platform base URL
- `Platform:ApiKey`: API key used to call the platform
- `Platform:PollIntervalSeconds`: polling interval in seconds

The app validates the most important settings at startup and fails fast if they are missing or malformed.

`Platform:PollIntervalSeconds` is intentionally configurable instead of hardcoded. In a real connector, different customer environments will have different trade-offs between freshness, platform load, and infrastructure cost, so I wanted the agent to be easy to tune without code changes.

### Run tests

From the repository root:

```bash
dotnet test candidate/SyncAgent/SyncAgent.sln
```

---

## Architecture Decisions

### Why a Worker Service

The challenge describes an always-on agent that polls for work, which is very close to how I would expect an on-prem connector to behave in a real product. A .NET Worker Service is a good fit because it gives a clean hosting model, dependency injection, configuration, logging, and graceful shutdown without needing extra plumbing. It also keeps the deployment model simple: a small process that can run close to the customer's system and stay synchronized with the central platform.

### Project structure

I split the solution into four projects:

- `SyncAgent.Core`: interfaces and shared models
- `SyncAgent.Infrastructure`: SQL access, HTTP client, handlers, and task validation
- `SyncAgent.Worker`: composition root and polling loop
- `SyncAgent.Tests`: unit tests

This keeps the boundaries simple:

- `Core` defines contracts
- `Infrastructure` implements them
- `Worker` wires everything together and coordinates execution

I intentionally kept the design lightweight. For a challenge of this size, I wanted clear separation without adding extra abstractions or layers that would not pay off. My goal was not to build a framework, but to build a small connector that is easy to understand, deploy, and extend with new task types.

### Handler-based task execution

Each supported task type has its own handler implementing `ISyncTaskHandler`:

- `GetCustomers`
- `GetProducts`
- `GetOrders`
- `GetProductInventory`

The worker asks the platform for the next task, validates it, and delegates execution through `SyncDispatcher`, which resolves handlers by `TaskType`. This keeps the polling loop simple and makes new task types easy to add later, which matters if the platform grows over time and the connector needs to support more sync operations.

### Why Dapper

I chose Dapper because this is a read-heavy integration against a database we do not own. The challenge is mainly about writing clear queries and shaping the returned payloads, so Dapper felt like the most direct and pragmatic choice. It keeps SQL explicit, makes the mapping easy to reason about, and avoids the setup cost of introducing a larger ORM for a small sync agent.

### Task validation

I added a small validation step before dispatching work. It currently checks required task fields and validates the `modifiedSince` parameter format. This is enough to reject malformed tasks early, fail predictably, and protect the database layer from obviously bad input without introducing a larger validation framework.

---

## Security Measures

- **Parameterized SQL queries**: all SQL uses parameters such as `@ModifiedSince`, which prevents SQL injection.
- **API key outside code paths**: the platform API key is read from configuration and attached through a delegating handler instead of being duplicated in request code.
- **Fail-fast configuration validation**: startup validates `BaseUrl`, `ApiKey`, polling interval, and the database connection string so bad configuration does not fail later in the polling loop.
- **Task validation before execution**: malformed tasks are rejected and reported as failed instead of being executed blindly.
- **Task-type whitelist by registration**: only registered handlers can be executed.

---

## Testing Strategy

I focused tests on the parts that are most important for a small background agent:

- worker orchestration
- dispatcher behavior
- task validation
- parameter propagation from tasks into handlers

The current suite covers:

- no task available
- known task processed successfully
- unknown task type reported as failure
- handler exception reported as failure
- invalid `modifiedSince` rejected before handler execution
- dispatcher routing and error handling
- validator rules for missing or invalid task data
- `modifiedSince` forwarding from handlers to the repository

I kept the test suite intentionally compact. For a challenge like this, I think a smaller set of focused tests is better than trying to unit test every trivial branch. I wanted the tests to reflect the parts that matter most in a connector: orchestration, validation, and correct handoff from the platform task into the query layer.

In addition to the automated tests, I also manually verified the real operating flow on a Windows VM against the provided `SyncPlatform` and a real `AdventureWorks2025` database. I validated all four task types end to end (`GetCustomers`, `GetProducts`, `GetOrders`, and `GetProductInventory`) to confirm that the full loop works as expected in practice: poll, validate, query, and post result.

With more time, I would add:

- integration tests against a real AdventureWorks instance
- tests for `PlatformClient` using a mocked `HttpMessageHandler`
- end-to-end tests that exercise the full agent against the provided platform simulator

---

## Known Limitations

- No retry or exponential backoff strategy for platform calls yet
- No persisted watermark/state between runs; each task is treated independently
- No batching/streaming for large result sets
- Single-task sequential processing
- Logging is intentionally basic; there are no metrics or health checks

These are trade-offs I made to keep the implementation small and readable for the scope of the exercise. If I were moving this from challenge scope toward production scope, the next things I would prioritize are operational concerns such as retries/backoff, persisted sync state, and better observability.

---

## AI Tools Used

- Claude / Cursor for code review, refactoring suggestions, and documentation polishing
- AI assistance to challenge my design choices, improve validation/error handling, and refine the test suite

I reviewed and kept only the changes that matched the level of simplicity I wanted for the final solution.

---

## Time Spent

Approximate breakdown:

- ~30 min understanding the challenge and deciding on the solution shape
- ~75 min implementing the worker, handlers, HTTP client, and SQL queries
- ~30 min testing and cleanup
- ~20 min final review and submission write-up

**Total: ~2.5 hours**

---

## Feedback

The challenge is well scoped and realistic. It tests a good mix of practical SQL work, service orchestration, and code organization without being artificially large.

What I liked most is that the problem feels close to a real integration task rather than a purely academic exercise. The platform simulator also helps a lot because it makes the request/response flow easy to validate while developing.
# Challenge Submission

## Candidate

- **Name:** Maximiliano Schmidt
- **Date:** 2026-03-12

---

## How to Run

### Prerequisites

Before running the agent, make sure you have:

- .NET 10 SDK installed
- SQL Server with `AdventureWorks2025` restored (details in `README.md`)
- The SyncPlatform WPF app running:

```bash
cd src/SyncPlatform
dotnet run --project SyncPlatform
```

### Run the Sync Agent

```bash
cd candidate/SyncAgent
dotnet run
```

The agent polls `http://localhost:5100` every 5 seconds.

Once the SyncPlatform app is running, you can click any of the buttons in the UI to enqueue a task. The agent will pick it up automatically and send the result back to the platform.

### Run Tests

```bash
cd candidate
dotnet test SyncAgent.slnx
```

### Configuration

You can change the main settings in `candidate/SyncAgent/appsettings.json`:

- `ConnectionStrings.AdventureWorks`: SQL Server connection string
- `SyncAgent.PlatformBaseUrl`: platform base URL (default: `http://localhost:5100`)
- `SyncAgent.ApiKey`: API key (default: `candidate-test-key-2026`)
- `SyncAgent.PollIntervalSeconds`: polling interval in seconds (default: `5`)

---

## Architecture Decisions

### Worker Service

I used a Worker Service instead of a plain console app because it already gives me the background execution model I needed. It also makes things like DI, logging, configuration, and cancellation handling straightforward, which fits this kind of polling agent pretty well.

### Handler-based task processing

Each task type has its own handler:

- `GetCustomers`
- `GetProducts`
- `GetOrders`
- `GetProductInventory`

All of them implement `ITaskHandler`, and a `TaskHandlerFactory` resolves the correct one based on the task type.

The main reason I went this way was to avoid putting all the logic into one big class or a switch statement. If I had to add another task later, I would just create a new handler and register it in DI.

### Dapper

I went with Dapper here because the challenge is mostly query-driven and the data retrieval is very explicit. Since AdventureWorks is not my database and I wanted to keep full control over the SQL being executed, Dapper felt like the most direct option, I mostly work with SP instead of plain queries, but this is just a test and isn't justified.

### Repository

All database access is inside `AdventureWorksRepository`, behind `IAdventureWorksRepository`.

That keeps the handlers focused on shaping the response instead of dealing with SQL directly, and it also makes unit testing easier because the repository can be mocked.

### Orders mapping

For `GetOrders`, the query returns flat rows, one per order line. I grouped them in memory to build the final nested structure with the `orderDetails` collection.

## Security Measures

A few basic security-related things are covered:

1. **API key is not hardcoded in the code**  
   It is read from configuration through `IOptions<SyncAgentOptions>`. For a real production setup, I would move it to environment variables or a secrets store.

2. **API key is attached through the configured HttpClient**  
   I set it once in DI using the default request headers, so I don’t have to remember to add it manually on every request.

3. **Task type is validated before execution**  
   The worker checks whether a handler exists for the requested task type. If not, it returns a failed result instead of crashing or ignoring the task.

4. **SQL is parameterized**  
   Queries use Dapper parameters such as `@ModifiedSince`, so there is no string interpolation in SQL.

5. **Cancellation tokens are respected**  
   Async operations propagate the `CancellationToken`, so the service can stop cleanly.

---

## Testing Strategy

### What is covered

The tests currently cover:

- the 4 task handlers
- the task handler factory
- the order grouping logic in `GetOrdersHandler`

For the handlers, I verified things like:

- correct `TaskType`
- correct `RecordCount`
- `"completed"` status
- forwarding of the `modifiedSince` parameter

For `GetOrdersHandler`, I specifically tested the grouping behavior to make sure multiple flat rows for the same order end up as a single order with multiple detail items.

For `TaskHandlerFactory`, I tested:

- resolution by task name
- case-insensitive matching
- `HasHandler`
- exception for unsupported task types

### What I would improve next

If I kept extending this, the next things I would add are:

- integration tests against a real AdventureWorks database (or LocalDB)
- tests for `PlatformApiClient` using a mocked `HttpMessageHandler`
- worker-level tests for polling scenarios such as:
  - no task available
  - failed task execution
  - cancellation / shutdown behavior

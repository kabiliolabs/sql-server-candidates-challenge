# Challenge Submission

## Candidate

- **Name:** Esteban Lázaro
- **Date:** 2026-07-03

---

## How to Run

**Prerequisites:** .NET 8 runtime/SDK and a local SQL Server with the `AdventureWorks2025`
database restored (see the root `README.md`).

1. **Configure the connection** (defaults already target a local instance with
   Windows auth). Override anything sensitive without editing source:

   ```bash
   # from candidate/TaskExecutionService
   dotnet user-secrets set "Database:ConnectionString" "Server=localhost;Database=AdventureWorks2025;Integrated Security=True;Encrypt=False;TrustServerCertificate=True"
   dotnet user-secrets set "Platform:ApiKey" "candidate-test-key-2026"
   ```

   Or via environment variables: `Database__ConnectionString`, `Platform__ApiKey`,
   `Platform__BaseUrl`.

2. **Start the platform** (provided test app):

   ```bash
   cd src/SyncPlatform
   dotnet run --project SyncPlatform
   ```

3. **Start the sync agent:**

   ```bash
   cd candidate/TaskExecutionService
   dotnet run
   ```

4. Click a button in the platform UI (Customers / Products / Orders / Inventory).
   The agent picks up the task, queries AdventureWorks, and posts the result —
   the platform log shows `200 OK — Result accepted`.

**Run the tests:**

```bash
cd candidate/TaskExecutionService.Tests
dotnet test                 # unit tests only (DB-backed tests are skipped)
SYNC_DB_TESTS=1 dotnet test  # also runs integration + end-to-end tests (needs the DB)
# or 
set "SYNC_DB_TESTS=1" && dotnet test # if you are using a cmd in windows
```

---

## Architecture Decisions

**Project:** a .NET 8 **Worker Service** (`BackgroundService`) — the natural fit for
an always-on polling agent, with built-in hosting, DI, configuration and logging.

**Layered, single-responsibility structure:**

- `Platform/` — the HTTP contract: `SyncTask`/`SyncResult` DTOs, `IPlatformClient`
  (typed `HttpClient`) that does GET next-task / POST result. A single
  `JsonSerializerOptions` (camelCase) is the one source of truth for wire format.
- `Sync/` — orchestration. `ISyncTaskExecutor` is the extension point; `TaskDispatcher`
  routes a task to the executor whose `TaskType` matches, using a dictionary built
  from **all executors registered in DI**.
- `Sync/Executors/` — one class per task type (`GetCustomers`, `GetProducts`,
  `GetOrders`, `GetProductInventory`), each owning its SQL.
- `Data/` — `IDbConnectionFactory` abstracts connection creation so executors are
  provider-agnostic and testable.
- `Models/` — the result DTOs shaped exactly to the contract.
- `Worker` — the poll → dispatch → post loop.

**Why this shape — reusability (the key evaluation point):** adding a new task type
is **one new executor class + one line in `Program.cs`**. Nothing else changes —
the dispatcher, worker and client are all closed for modification (Open/Closed).

**Dapper** over raw ADO.NET: parameterized queries with concise, safe object mapping
and far less boilerplate, without the weight of a full ORM for read-only queries.

**`GetOrders`** runs two set-based queries (headers + details) filtered by the same
predicate and groups details in memory — avoiding an N+1 query per order.

**`TimeProvider`** is injected so `executedAt` timestamps are deterministic in tests.

---

## Security Measures

- **Secrets out of source:** API key and connection string are configuration-driven
  and overridable via user-secrets / environment variables; nothing sensitive is
  hardcoded. Startup validation (`ValidateOnStart`) fails fast if they are missing.
- **SQL injection prevention:** every query is parameterized (`@modifiedSince`);
  task input is never concatenated into SQL.
- **Input validation:** unknown task types are rejected, and `modifiedSince` is parsed
  and validated to a UTC `DateTime` **before** any DB access. Invalid input becomes a
  clean `failed` result, never an exception that stops the agent.
- **No information leakage:** unexpected exceptions are logged in full locally but the
  `errorMessage` returned to the platform is generic ("internal error"). Only safe
  validation messages are surfaced. (Covered by a unit test asserting internal detail
  does not leak.)
- **Bounded work:** a per-query `CommandTimeout` caps long-running queries.
- **Least privilege (recommended for prod):** the agent only issues `SELECT`s and
  should run under a dedicated read-only SQL login; connection encryption settings are
  explicit in the connection string.

---

## Testing Strategy

**24 tests total** (xUnit + FluentAssertions + Moq):

- **Unit (19, no DB required):**
  - `SyncParameters` — valid/missing/malformed `modifiedSince`.
  - `TaskDispatcher` — correct routing, unknown type → failed, invalid params → safe
    message, executor exception → generic message (no leak), missing id guard.
  - `PlatformClient` — 204 → null, 200 → parsed task, 401 → throws, POST emits correct
    camelCase body (incl. `errorMessage:null`) and throws on 400.
  - `SyncResult` factories — status/data/recordCount invariants.
- **Integration (4, opt-in via `SYNC_DB_TESTS=1`):** each executor runs against real
  AdventureWorks and reproduces the documented sample rows (customer 11000, product
  680, order 43659 with its 12 nested line items, inventory product 1).
- **End-to-end (1, opt-in):** a real in-process HTTP listener stands in for the
  platform while the real `PlatformClient` + dispatcher + DB run a full
  poll → execute → post cycle; the posted payload is asserted to be a valid `completed`
  result.

DB-backed tests are gated behind an environment variable so the unit suite stays green
in CI without a database (19 passed / 5 skipped).

**With more time:** tests for HTTP transient-fault retry (Polly), cancellation during a
long query, and contract tests generated from the sample payload files.

---

## Known Limitations

- **No retry/backoff policy** beyond a fixed delay after a failed cycle. A production
  agent would use Polly for exponential backoff on transient HTTP/SQL faults.
- **No task acknowledgement / idempotency:** if the agent crashes after querying but
  before posting, the task is simply re-polled. Fine for this platform (queue-based) but
  worth an idempotency key in production.
- **Whole-result-set in memory:** results are materialized and posted in one payload.
  For very large syncs, streaming/paging would be needed.
- **`GetCustomers` picks one address/email/phone** (prefers Home address) to match the
  one-row-per-customer sample shape; multi-address customers aren't expanded.
- The provided platform sends the same `modifiedSince` for all tasks; incremental
  watermark tracking (persisting the last successful sync time per task type) would be a
  natural next step.

---

## AI Tools Used

- **Claude (Claude Code)** was used throughout:
  - Explored the repo and the API contract / sample payloads to derive the exact result
    shapes.
  - **Connected to the live AdventureWorks2025 database via `sqlcmd`** to discover the
    schema and **validate every query against real data** before writing C# — the
    customer person/address/email/phone joins, the product category/subcategory joins,
    the orders header + line-item structure (discovering that a customer can carry both a
    `PersonID` and a `StoreID`), and the inventory location joins.
  - Generated the Worker/dispatcher/executor scaffolding, the typed HTTP client, and the
    unit/integration/E2E tests, then iterated until the suite was green.
- All generated SQL and code were verified against the running database and the passing
  test suite rather than accepted blindly.

---

## Time Spent

- Exploring repo, API contract and AdventureWorks schema: ~30 min
- Designing the structure (executors/dispatcher/client) and validating SQL live: ~30 min
- Implementation (agent + all four executors): ~40 min
- Tests (unit + integration + end-to-end) and verification: ~30 min
- Documentation / submission: ~10 min

*(~2h 20m total.)*

---

## Feedback

Clear, realistic and well-scoped challenge. The provided sample payloads made the target
data shapes unambiguous, and leaving the schema exploration to the candidate mirrors real
onboarding onto an unfamiliar client database. One small note: the WPF test app requires
an interactive desktop to enqueue tasks, so a headless/CLI way to enqueue a task (or a
documented enqueue endpoint) would make automated end-to-end testing easier.

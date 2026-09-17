# Implementation Plan — SQL Server Sync Agent

Status: **Implemented and verified end-to-end, robustness pass included** (all
four task types tested live against the real database and the running
SyncPlatform simulator, both before and after adding Mediator, log4net, and HTTP
resilience). This document is a living record of the design — updated as it
evolved, not just the pre-code plan. See §11 for exactly what's done vs. what's
left for more time.

---

## 1. Goal

A .NET 8 Worker Service that polls `GET /api/sync/next-task`, executes the
requested query against the local `AdventureWorks2025` database, and posts the
result to `POST /api/sync/result`. Four task types: `GetCustomers`,
`GetProducts`, `GetOrders`, `GetProductInventory`.

---

## 2. Solution structure

```
candidate/
├── SyncAgent.sln                  (classic .sln format — SDK 10 defaults to .slnx,
│                                    converted back for .NET 8 SDK compatibility)
├── src/
│   ├── SyncAgent.Core/            (no I/O — portable to .NET Framework 4.8)
│   │   ├── Tasks/                 task type DTOs, per-record types, SyncTask/SyncResult
│   │   ├── Abstractions/          ISyncQueryRepository<TRecord>, ISyncPlatformClient
│   │   ├── Configuration/         SyncAgentOptions (+ validator), DI registration
│   │   ├── Queries/                IRequest<IReadOnlyList<object>> per task type (Mediator)
│   │   ├── Handlers/              one IRequestHandler<TQuery,...> per task type — thin,
│   │   │                          each just calls its ISyncQueryRepository<TRecord>
│   │   └── Dispatch/              SyncTaskDispatcher (taskType string → query, via
│   │                              Mediator's ISender; never throws),
│   │                              LoggingPipelineBehavior (cross-cutting duration/count log)
│   ├── SyncAgent.Infrastructure/  (Dapper, HttpClient + resilience, log4net wiring)
│   │   ├── Data/                  connection factory, SqlOptions, one Repository +
│   │   │                          SQL query text + (where needed) an internal Row type
│   │   │                          per task type
│   │   └── Platform/              SyncPlatformClient (typed HttpClient), PlatformOptions,
│   │                              ResilienceOptions, ApiKeyDelegatingHandler, wire DTOs
│   └── SyncAgent.Worker/          BackgroundService host, composition root (Program.cs),
│                                  log4net.config
└── tests/
    ├── SyncAgent.Core.Tests/      dispatcher tests on a real Mediator pipeline
    ├── SyncAgent.Infrastructure.Tests/  contract tests vs docs/sample-payloads/*.json;
    │                                    SQL integration tests gated by env var (skip
    │                                    without DB)
    └── SyncAgent.Worker.Tests/    polling loop behavior with a fake ISyncPlatformClient
                                   and a FakeTimeProvider (deterministic, no real delays)
```

**Query-strategy pairing:** each handler pairs with exactly one narrow
`ISyncQueryRepository<TRecord>` (see §4) — adding a new task type means one record
type, one query+repository implementation, one handler class, and one DI line.
Nothing else changes: not the dispatcher, not the worker loop, not the HTTP client.

---

## 3. Task types, queries, and verified filters

All four queries filter on `ModifiedDate >= @modifiedSince`, parameterized, with a
`TOP(@n)` cap (default 5000, configurable via `SyncAgent:MaxRowsPerTask`).

| Task | Filter column | Row count returned (>= 2025-01-01, no cap) |
|---|---|---|
| `GetCustomers` | `Sales.Customer.ModifiedDate` | **19,119** (see correction below) |
| `GetProducts` | `Production.Product.ModifiedDate` | 504 |
| `GetOrders` | `Sales.SalesOrderHeader.ModifiedDate` | 12,304 |
| `GetProductInventory` | `Production.ProductInventory.ModifiedDate` | 609 |

**Known trap (verified, do not repeat):** filtering `GetProductInventory` by
`Production.Product.ModifiedDate` instead of `ProductInventory.ModifiedDate` gives
1,069 rows — wrong join, called out in a query comment.

### GetCustomers
Join: `Customer → Person → EmailAddress`, `Customer → Person → BusinessEntityAddress
→ Address → StateProvince → CountryRegion`. Verified against the sample payload's
two customers (11000 Jon Yang, 11001 Eugene Huang, both Australia).

**Correction to the pre-code planning notes:** those notes verified the customer
join against only those 2 example customers and concluded 19,820 was the right row
count, explicitly dismissing another candidate's reported 19,119 as wrong. Checking
against the full table during implementation found two things the 2-row spot-check
missed:
- **701 of the 19,820 filtered rows have `PersonID IS NULL`** — store/B2B accounts
  with no linked `Person` row (no name, email, or address). An inner join to get
  person data correctly excludes them, landing at 19,820 − 701 = **19,119** — the
  other candidate's number was right.
- **~24 customers have more than one row in `BusinessEntityAddress`** (multiple
  addresses). Deduped deterministically with `ROW_NUMBER()`, preferring the "Home"
  address type, to avoid duplicate customer rows in the output.

`phone` → always `null`, as an explicit mapping decision in
`CustomerRepository.MapToRecord` — not a literal baked into the SQL text. See §4.

**Bug found in manual testing (fixed):** Dapper's constructor-based materialization
can't bridge `DateTime` (from a SQL `datetime` column) to a `DateTimeOffset`
constructor parameter — it silently falls back to requiring a parameterless
constructor and throws. Hit this on `GetProducts` first (`ProductRecord.ModifiedDate`
is `DateTimeOffset`); the same issue would have hit `GetProductInventory` and
`GetOrders` too. Fixed by giving every repository an internal `*Row` type with
`DateTime` for date columns, converted to `DateTimeOffset` explicitly in each
repository's mapping step — the same pattern already used for the `phone` field.

### GetProducts
`Production.Product` joined to `ProductSubcategory` → `ProductCategory` for
`category`/`subcategory`. Straightforward 1:1, no fan-out risk.

### GetOrders
Nested shape (header + `orderDetails[]`) — **two queries, assembled in memory**:
1. `SalesOrderHeader` (+ customer name via `Customer → Person`) filtered by
   `ModifiedDate`, capped at `TOP(@n)`. (Verified: 0 of the 12,304 matching orders
   belong to a store/B2B customer, unlike GetCustomers — no null-name risk here.)
2. `SalesOrderDetail` for the resulting `SalesOrderID`s, joined to `Product` for
   name/number.

Never a single JOIN across header+detail — that would repeat the header once per
line item and make `TOP(@n)` cap the wrong thing (order lines instead of orders).

**Bug found in manual testing (fixed):** SQL Server caps a parameterized query at
2,100 parameters, and Dapper expands `WHERE SalesOrderID IN @Ids` into one
parameter per id. With `MaxRowsPerTask` defaulting to 5,000, the detail query for a
full-size `GetOrders` batch failed outright ("too many parameters"). Fixed by
chunking the order ids into batches of 2,000 and issuing multiple detail queries,
merged in memory before assembly — same two-query-then-assemble shape, just batched
on the second query.

### GetProductInventory
`Production.ProductInventory` joined to `Production.Product` and
`Production.Location`, filtered on `ProductInventory.ModifiedDate` (not
`Product.ModifiedDate` — see trap above).

---

## 4. Dispatcher, handlers, and repository design

Dispatch now runs on **Mediator** (martinothamar — source-generated, MIT licensed;
not MediatR, whose recent major versions require a commercial license). Each task
type is an `IRequest<IReadOnlyList<object>>` record in `Core/Queries/` with a
matching `IRequestHandler<TQuery, IReadOnlyList<object>>` in `Core/Handlers/`.
`SyncTaskDispatcher` still needs a small `taskType`-string → query-construction map
(`SyncTaskDispatcher.QuerySenders`) — the platform's `taskType` arrives as an
untyped runtime string, but Mediator dispatch is compile-time typed, so that map is
the one place a new task type costs more than "one class, one DI line": one
dictionary entry, constructing that task's query from the validated request plus
the configured row cap.

- **Segregated repository interfaces:** `ISyncQueryRepository<TRecord>` (Core) is
  one generic interface closed over each task's record type
  (`ISyncQueryRepository<CustomerRecord>`, `ISyncQueryRepository<ProductRecord>`,
  ...) rather than one fat repository interface with a method per task. Core
  depends only on this interface; all Dapper/SQL specifics live in the
  Infrastructure implementation, which is what keeps Core free of I/O
  dependencies (the .NET Framework 4.8 portability argument in §10 actually holds).
- **Query-strategy pairing:** each handler (`Core/Handlers/`) takes exactly one
  `ISyncQueryRepository<TRecord>` and calls it once — see §2.
- **Result type instead of exceptions for expected failures:** `SyncTaskDispatcher`
  handles the two *expected* failure modes — an unrecognized `taskType`, or a
  missing/malformed `modifiedSince` — with plain `if` checks that return a
  `SyncResult.Failed(...)`, never by throwing. Exceptions are reserved for what's
  genuinely unexpected (a handler's repository call throwing — SQL down, timeout,
  etc.) and are caught once, centrally, in the dispatcher's `try/catch`, also
  converted to `SyncResult.Failed(...)`. Either way the dispatcher never throws
  past itself, so the worker loop can always post something back.
- **Decorator/pipeline behavior for cross-cutting logging:**
  `LoggingPipelineBehavior<TRequest, TResponse>` (`Core/Dispatch/`) is a generic
  `IPipelineBehavior<TRequest, TResponse>` wrapping every Mediator request, logging
  duration and record count — metadata only, never the request/response payload.
  This is what moved the "how long did this take, how many records" concern out of
  each handler and into exactly one place; handlers only fetch and shape data.
  Verified live: `LoggingPipelineBehavior - GetCustomersQuery handled in 442ms
  (5000 records)` appeared correctly in the log during manual end-to-end testing.
- **Four explicit handler classes, not one generic one:** all four handlers end up
  the same shape (fetch via the repository, cast to `object`). Kept as four
  explicit classes rather than a `SyncTaskHandler<TRecord>` generic — matches the
  "abstract at the second occurrence, not the first" principle applied to the
  dispatcher/handler layer during planning, stays trivially readable, and a 5th
  task type is free to diverge (e.g. extra validation) without fighting a shared
  base.
- **Testing gotcha worth documenting:** `AddMediator()` is generated inside the
  Core assembly and auto-registers *every* real `IRequestHandler<,>` it finds
  there — and resolving `ISender` eagerly builds Mediator's dispatch table, which
  means all four real handlers' constructor dependencies must be satisfiable even
  in a unit test that only exercises one of them. `SyncTaskDispatcherTests` and
  `WorkerTests` both register no-op `ISyncQueryRepository<TRecord>` fakes for all
  four record types up front, then override just the handler under test.

---

## 5. Worker loop behavior

- `BackgroundService` polls `GET /api/sync/next-task` on a configurable interval.
- `204` → no-op, keep polling (not an error).
- `200` → dispatch to the matching handler.
- **Any exception** (unknown task type, SQL failure, timeout, serialization error)
  is caught at the top of the loop and posted as `status:"failed"` with
  `errorMessage` — the polling loop itself must never crash. There's no requeue in
  the simulator, so a task that's dequeued and never posted back is lost; this is
  called out explicitly in Known Limitations rather than hidden.
- Config validated fail-fast at startup: connection string, API key, base URL, and
  poll interval all checked before the host starts polling — see §7 for how.

---

## 6. HTTP client & resilience

**Implemented and verified against the live simulator** (happy path unaffected —
204s pass straight through, Polly telemetry visible at Trace/Info without extra
retries triggered on non-error responses).

- Typed `HttpClient` (`SyncPlatformClient`) registered via `HttpClientFactory`.
- Resilience pipeline via `Microsoft.Extensions.Http.Resilience`
  (`AddResilienceHandler("sync-platform", ...)`), all tunables configurable under
  `SyncAgent:Platform:Resilience` and validated at startup
  (`ResilienceOptionsValidator`):
  - **Rate limiter** (outermost) — `SlidingWindowRateLimiter`, configurable max
    requests/sec (`MaxRequestsPerSecond`, default 10) — a defense against a
    misconfigured poll interval or a retry storm hammering the platform,
    independent of anything the platform itself does.
  - **Retry** — transient failures (5xx, network errors), exponential backoff with
    jitter, configurable max attempts.
  - **Circuit breaker** — opens after a configurable failure ratio over a sampling
    window, stops hammering the platform while it's down.
  - **Timeout** — per-attempt, configurable.
  - Order mirrors Microsoft's own standard resilience handler (rate limiter →
    timeout/retry/circuit-breaker guarding each attempt), documented in the code
    comment in `ServiceCollectionExtensions.AddSyncAgentInfrastructure`.
- `X-Api-Key` attached via a `DelegatingHandler`, never logged.

---

## 7. Security measures

- API key and DB connection string read from **environment variables** only
  (`SYNCAGENT_DB_CONNECTION`, `SYNCAGENT_API_KEY`) — never hardcoded, never
  committed, never bound from `appsettings.json`. Non-secret settings (row cap,
  poll interval, command timeout, platform base URL) do come from
  `appsettings.json`, under a `SyncAgent` section.
- **Options pattern with fail-fast validation:** each settings group is its own
  options type — `SyncAgentOptions` (Core: `MaxRowsPerTask`, `PollIntervalSeconds`),
  `SqlOptions` (Infrastructure: `ConnectionString`, `CommandTimeoutSeconds`),
  `PlatformOptions` (Infrastructure: `BaseUrl`, `ApiKey`), `ResilienceOptions`
  (Infrastructure: retry/circuit-breaker/timeout/rate-limiter tunables) — each with
  a matching `IValidateOptions<T>` and registered with `.ValidateOnStart()`.
  `Program.cs` additionally resolves every `IOptions<T>.Value` explicitly right
  after `host.Build()`, in a small helper that catches `OptionsValidationException`
  and logs every individual failure via `ILogger` at `Critical` before exiting with
  a non-zero code — so a missing env var or a bad numeric setting produces one
  clear log line per problem (e.g. "Configuration error (SqlOptions):
  ConnectionString is required...") instead of a stack trace on the first task, or
  the host silently polling with an empty connection string.
- SQL 100% parameterized (`@modifiedSince`, `@n`, `@SalesOrderIds`) — no string
  concatenation.
- `taskType` allow-listed against the four known values; anything else fails
  cleanly instead of being interpolated into a query or reflected into a type.
- `TOP(@n)` row cap + SQL command timeout as defenses against a hostile or
  malformed task payload.
- No PII in logs (see below), and no secrets in logs (API key, connection string
  never logged even at Debug level).

---

## 8. Logging (log4net)

**Implemented.** log4net is the sole logging sink — `builder.Logging.ClearProviders()`
+ `AddLog4Net("log4net.config")` in `Program.cs`, so every log line (including the
framework's own `Microsoft.Hosting.Lifetime` and `HttpClientFactory` messages)
goes through one PII-safe config, on both console and a daily-rolling file
(`logs/syncagent.log`, 14-day retention).

- Metadata only, at every log level: task type, `taskId`, record counts,
  durations (via `LoggingPipelineBehavior`, see §4), HTTP status codes, exception
  types and messages.
- **Never logged:** row-level data (customer names, emails, addresses, product
  data), the connection string, or the API key. `log4net.config` documents this
  policy in a comment at the top — the config doesn't filter anything out, because
  nothing unsafe should ever reach it in the first place.
- `Polly` and `System.Net.Http.HttpClient` categories are tuned to `WARN` by
  default — both are metadata-only (no body content, so no PII risk) but noisy at
  `INFO` for normal operation.
- Verified live: the file sink correctly recorded a full poll → dispatch →
  complete cycle during manual testing, including the pipeline behavior's
  duration/record-count line.

---

## 9. Testing strategy

**17 tests, all passing** (`dotnet test SyncAgent.sln`). Split by project as
planned in §2:

- `SyncAgent.Core.Tests` (6): `SyncTaskDispatcherTests` — unknown task type and
  missing/malformed `modifiedSince` return `"failed"` without throwing; a handler
  exception is caught and becomes a `"failed"` result with the exception's
  message; a successful dispatch returns `"completed"` with `recordCount` matching
  the data length; multiple registered handlers resolve to the correct one by
  `taskType`. Built on a **real Mediator pipeline** (`ServiceCollection` +
  `AddMediator()` + test-only handler overrides), not a hand-rolled fake `ISender`
  — exercises the same handler-resolution path production uses.
- `SyncAgent.Infrastructure.Tests` (8):
  - `SamplePayloadContractTests` (4) — the JSON *shape* (property name set) of
    each record type, serialized with the same `JsonSerializerOptions` as
    `SyncPlatformClient`, compared against `docs/sample-payloads/result-*.json`'s
    first data element (and, for `GetOrders`, the nested `orderDetails[0]` too).
    Structure only, not literal values — the real DB won't match the doc's
    example rows exactly. All four passed on the first run, confirming the
    record types' property names line up with the documented contract exactly.
  - `RepositoryIntegrationTests` (4) — the real Dapper queries against a real
    database, gated by `SYNCAGENT_TEST_DB_CONNECTION` (a connection string env
    var) via a custom `[SkippableFact]` so `dotnet test` reports them as
    **skipped** (not failed) with no database available. Includes a regression
    guard pinning `GetProductInventory` to the correct 609-row count (filtering on
    `ProductInventory.ModifiedDate`) rather than the documented trap's 1,069.
- `SyncAgent.Worker.Tests` (3): the polling loop against a fake
  `ISyncPlatformClient`, driven by a `FakeTimeProvider` (`Worker` takes an
  optional `TimeProvider`, defaulting to the real clock in production) so poll
  cycles are triggered by explicitly advancing virtual time rather than waiting
  on real wall-clock delays — deterministic, not sensitive to CI scheduling
  jitter. Covers: the platform throwing on every poll doesn't crash the loop
  (`StartAsync`/`StopAsync` complete normally); a `204` never triggers a
  POST; a task with an unrecognized type still gets a `"failed"` result posted
  back exactly once.

**Not yet covered:** the log4net PII policy is enforced by convention (every log
call in the codebase only passes metadata — verifiable by inspection) rather than
by an automated guard test; a test that intercepts `ILogger` calls and asserts no
row-level data is ever passed would be the next addition with more time.

---

## 10. Known limitations (declared upfront, not hidden)

- No real pagination — `TOP(@n)` caps large result sets (e.g. ~19.1k customers,
  ~12.3k orders); a production sync would need cursor-based paging.
- No requeue/idempotency beyond the simulator's own behavior — a task lost
  between dequeue and POST (e.g. process crash) is gone; a real system would need
  ack/lease semantics.
- `phone` is always `null` — no matching field exists in AdventureWorks.
- Store/B2B customer accounts (no linked `Person` row) are out of scope for
  `GetCustomers` — the sample payload has no shape for a business account.
- Built and tested against .NET 8 only; Core has no I/O dependencies (now more
  rigorously true after moving Dapper entirely into Infrastructure repositories)
  and is portable to .NET Framework 4.8 for Kabilio's actual on-prem client
  runtimes, but that portability isn't verified in code for this submission.
- No automated PII-policy guard test (see §9) — the "no row data in logs" rule is
  enforced by convention/code review, not a test that would fail if violated.
- No requeue/idempotency test coverage beyond what the manual testing and
  `WorkerTests` demonstrate for the loop-level behavior.

---

## 11. Status and next steps

**Done — MVP:** solution scaffolded, all four task types implemented and manually
verified end-to-end against the live database and the running SyncPlatform
simulator. Two real bugs found during that manual testing (Dapper's
`DateTime`/`DateTimeOffset` constructor mismatch; SQL Server's 2,100-parameter
limit hit by `GetOrders`' detail query) were fixed and are documented in §3. One
correction to the pre-code planning notes (customer count 19,820 → 19,119) is
documented there too.

**Done — robustness pass:** Mediator (with a logging pipeline behavior), log4net
(sole logging sink, PII-safe by policy, console + rolling file), HTTP resilience
(rate limiter, retry, circuit breaker, timeout — all configurable and validated at
startup), and a 17-test suite (unit, contract, and gated SQL integration). Verified
together, live, against the real simulator and database one more time after all of
this landed — same correct end-to-end behavior, now with the Mediator pipeline
behavior's duration/record-count line and log4net's file sink both confirmed
working in that same run.

**Not done — candidates for more time, not required for a working submission:**
- .NET Framework 4.8 multi-targeting (argued in writing in §10, not built — this
  was a deliberate planning decision, not a shortfall).
- Real pagination beyond the `TOP(@n)` cap (§10).
- Ack/lease-based requeue semantics (§10).
- The automated PII-policy guard test mentioned in §9.
- `CHALLENGE_SUBMISSION.md` is still the empty template — needs to be filled in
  before this is ready to hand in, regardless of any further code changes.

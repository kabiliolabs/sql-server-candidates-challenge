# Implementation Plan — SQL Server Sync Agent

Status: **MVP implemented and verified end-to-end** (all four task types tested
live against the real database and the running SyncPlatform simulator). This
document is now a living record of the design — updated as it evolved, not just
the pre-code plan. Robustness work (Mediator, log4net, HTTP resilience/throttling)
comes next.

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
│   │   ├── Abstractions/          ISyncTaskHandler, ISyncQueryRepository<TRecord>,
│   │   │                          ISyncPlatformClient
│   │   ├── Configuration/         SyncAgentOptions (+ validator), DI registration
│   │   ├── Handlers/              one ISyncTaskHandler per task type — thin, each just
│   │   │                          calls its ISyncQueryRepository<TRecord>
│   │   └── Dispatch/              SyncTaskDispatcher (allow-list + never throws)
│   ├── SyncAgent.Infrastructure/  (Dapper, HttpClient, log4net wiring)
│   │   ├── Data/                  connection factory, SqlOptions, one Repository +
│   │   │                          SQL query text + (where needed) an internal Row type
│   │   │                          per task type
│   │   └── Platform/              SyncPlatformClient (typed HttpClient), PlatformOptions,
│   │                              ApiKeyDelegatingHandler, wire DTOs
│   └── SyncAgent.Worker/          BackgroundService host, composition root (Program.cs)
└── tests/
    ├── SyncAgent.Core.Tests/      handler dispatch, validation, PII-safety of log calls
    ├── SyncAgent.Infrastructure.Tests/  contract tests vs docs/sample-payloads/*.json;
    │                                    SQL query tests gated by env var (skip without DB)
    └── SyncAgent.Worker.Tests/    polling loop behavior with fake HttpMessageHandler
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

**Current state (MVP):** a plain `SyncTaskDispatcher` in Core resolves
`ISyncTaskHandler` by `taskType` from an allow-list built from DI-registered
handlers — no Mediator library wired in yet. The Mediator (martinothamar,
source-generated, MIT licensed — not MediatR, whose recent major versions require
a commercial license) is still the intended library for the robustness pass; the
handler shape below (`ExecuteAsync(request, ct)`) was deliberately kept close to
`IRequestHandler<TRequest, TResponse>` so swapping the dispatcher for
`ISender.Send()` later is a mechanical change, not a redesign.

- **Segregated repository interfaces:** `ISyncQueryRepository<TRecord>` (Core) is
  one generic interface closed over each task's record type
  (`ISyncQueryRepository<CustomerRecord>`, `ISyncQueryRepository<ProductRecord>`,
  ...) rather than one fat repository interface with a method per task. Core
  depends only on this interface; all Dapper/SQL specifics live in the
  Infrastructure implementation, which is what keeps Core free of I/O
  dependencies (the .NET Framework 4.8 portability argument in §10 actually holds).
- **Query-strategy pairing:** each `ISyncTaskHandler` (Core/Handlers/) takes exactly
  one `ISyncQueryRepository<TRecord>` and calls it once — see §2.
- **Result type instead of exceptions for expected failures:** `SyncTaskDispatcher`
  handles the two *expected* failure modes — an unrecognized `taskType`, or a
  missing/malformed `modifiedSince` — with plain `if` checks that return a
  `SyncResult.Failed(...)`, never by throwing. Exceptions are reserved for what's
  genuinely unexpected (a handler's repository call throwing — SQL down, timeout,
  etc.) and are caught once, centrally, in the dispatcher's `try/catch`, also
  converted to `SyncResult.Failed(...)`. Either way the dispatcher never throws
  past itself, so the worker loop can always post something back.
- **Decorator/pipeline behaviors for cross-cutting concerns (planned, not yet
  implemented):** once Mediator is wired in, per-task logging (duration, record
  count) and future concerns like metrics should be `IPipelineBehavior<TRequest,
  TResponse>` wrapping every handler, rather than logging calls embedded inside
  each handler. Deferred to the robustness pass so it lands alongside the actual
  Mediator migration instead of being built twice.
- **Four explicit handler classes, not one generic one:** all four handlers end up
  the same shape (fetch via the repository, cast to `object`). Kept as four
  explicit classes rather than a `SyncTaskHandler<TRecord>` generic — matches the
  "abstract at the second occurrence, not the first" principle applied to the
  dispatcher/handler layer during planning, stays trivially readable, and a 5th
  task type is free to diverge (e.g. extra validation) without fighting a shared
  base.

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

- Typed `HttpClient` (`SyncPlatformClient`) registered via `HttpClientFactory`.
- Resilience pipeline via `Microsoft.Extensions.Http.Resilience`
  (`AddResilienceHandler`), covering:
  - **Retry** — transient failures (5xx, network errors), exponential backoff.
  - **Circuit breaker** — stops hammering the platform if it's down.
  - **Timeout** — per-attempt and total.
  - **Rate limiter** — configurable max requests/sec self-throttle on both polling
    and result-posting, so a misconfigured poll interval or a retry storm can't
    hammer the platform.
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
  `PlatformOptions` (Infrastructure: `BaseUrl`, `ApiKey`) — each with a matching
  `IValidateOptions<T>` and registered with `.ValidateOnStart()`. `Program.cs`
  additionally resolves every `IOptions<T>.Value` explicitly right after
  `host.Build()`, in a small helper that catches `OptionsValidationException` and
  logs every individual failure via `ILogger` at `Critical` before exiting with a
  non-zero code — so a missing env var or a bad numeric setting produces one clear
  log line per problem (e.g. "Configuration error (SqlOptions): ConnectionString is
  required...") instead of a stack trace on the first task, or the host silently
  polling with an empty connection string.
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

- Metadata only, at every log level: task type, `taskId`, record counts,
  durations, HTTP status codes, exception types and messages.
- **Never logged:** row-level data (customer names, emails, addresses, product
  data), the connection string, or the API key.
- Log calls are covered by a Core-level test asserting handler results aren't
  passed whole into logger calls (guards against a future accidental
  `_logger.LogDebug(result)`-style regression).

---

## 9. Testing strategy

- **xUnit**, split by project as shown in §2.
- `SyncAgent.Core.Tests`: dispatcher resolves the right handler per task type;
  unknown task type is rejected; validation of `modifiedSince` parsing.
- `SyncAgent.Infrastructure.Tests`:
  - Contract tests — shape of each handler's output JSON compared against
    `docs/sample-payloads/result-*.json` (structure/field names, not literal
    values, since the real DB won't match the doc's example rows exactly).
  - SQL integration tests gated by an environment variable (e.g.
    `SYNCAGENT_TEST_DB=1`) so `dotnet test` passes with no database available.
- `SyncAgent.Worker.Tests`: polling loop against a fake `HttpMessageHandler` —
  204 keeps polling, 200 dispatches, exceptions during dispatch still POST
  `status:"failed"` and the loop continues.

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
- No unit/integration tests written yet — next after this MVP milestone.

---

## 11. Status and next steps

**Done:** solution scaffolded, all four task types implemented and manually
verified end-to-end against the live database and the running SyncPlatform
simulator (poll → query → post, correct `completed`/`failed` status, correct
`recordCount`, payload shapes matching the sample payloads). Two real bugs found
during that manual testing (Dapper's `DateTime`/`DateTimeOffset` constructor
mismatch; SQL Server's 2,100-parameter limit hit by `GetOrders`' detail query) were
fixed and are documented in §3. One correction to the pre-code planning notes
(customer count 19,820 → 19,119) is documented there too.

**Deferred to the robustness pass (see §4, §6, §8):** Mediator, log4net, HTTP
resilience (retry/circuit-breaker/timeout/rate-limiter), and the test suite in §9.

# Challenge Submission

## Candidate

- **Name:** Ghenadie Cojuhari
- **Date:** 2026-07-09

---

## How to Run

Prerequisites: .NET 8 runtime, and SQL Server with `AdventureWorks2025` restored.

```bash
# 1. Start the platform test app (WPF). It serves http://localhost:5100.
dotnet run --project src/SyncPlatform/SyncPlatform

# 2. Start the agent.
cd candidate
dotnet run --project src/SyncAgent
```

Then click **Sync Customers / Products / Orders / Inventory** in the platform window.
The agent picks each task up within one poll interval (5s), queries the database and posts
the result.

**Configuration.** `candidate/src/SyncAgent/appsettings.json` holds the connection string,
`Agent:PollInterval`, `Agent:MaxRecords` and `Platform:BaseAddress`. Anything can be overridden
by environment variable — the API key in particular:

```bash
export Platform__ApiKey="candidate-test-key-2026"                              # or user-secrets
export ConnectionStrings__AdventureWorks="Server=localhost,1433;Database=..."  # any instance
```

The challenge's throwaway key is committed in `appsettings.Development.json` only, so
`dotnet run` works with no setup. A real key would never live in a file.

**Tests.**

```bash
cd candidate
dotnet build SyncAgent.sln --nologo          # 0 warnings, 0 errors
dotnet test  SyncAgent.sln --nologo          # 169 unit tests; coverage collected automatically
dotnet test  SyncAgent.sln --filter Category=Integration   # 16 tests; needs a live database
```

Integration tests are excluded from the default run. They read their connection string from
`tests/SyncAgent.Tests/appsettings.Integration.json`, overridable with
`ConnectionStrings__AdventureWorks` — they do not assume `.\SQLEXPRESS`.

---

## Architecture Decisions

**Worker Service, not a console app.** The brief asks for an always-on application. The generic
host already provides DI, layered configuration, structured logging and graceful shutdown on
SIGTERM. A console app means writing those four things by hand.

**Dapper over raw `SqlDataReader`.** Each handler ends up around fifteen lines, which is what makes
the reusable-handler pattern visible rather than buried in column ordinals and manual mapping.

**Layout.**

```text
Platform/     typed HttpClient, the two endpoint contracts, JSON policy
Validation/   the trust boundary: nothing untrusted passes it
Sync/         ISyncTaskHandler, the dispatcher, the four handlers, order grouping
Data/         ISqlQueryExecutor, the four SQL constants
Dto/          the wire shapes
Worker.cs     the polling loop
```

**Adding a fifth task type is one class and one line.** `SyncTaskDispatcher` builds its routing
table from the injected `IEnumerable<ISyncTaskHandler>`, keyed on each handler's `TaskType` with
`StringComparer.OrdinalIgnoreCase`. So the set of registered handlers *is* the task-type allowlist.
A separate hand-maintained list of legal types was the obvious alternative, and was rejected: two
lists drift, and the drift fails open. Three of the four handlers are literally
`ProjectionSyncTaskHandler<TRow>` plus a task type and a SQL constant.

**The polling loop sleeps only when the queue is empty.** After handling a task it polls again
immediately, so a backlog drains at full speed while an idle agent costs one request per interval.
It waits via `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` rather than a bare `Task.Delay`,
which is what lets the loop tests assert "waits one poll interval" by refusing to advance a fake
clock instead of sleeping. No production code calls `DateTime.UtcNow`.

**The dispatcher is the failure boundary.** A handler that throws becomes a `failed` result, never
an escaped exception, so one poisonous task cannot take down an agent that nobody is watching.
Cancellation is the deliberate exception: during shutdown it propagates, because reporting `failed`
to the platform would be a lie.

**Validation has three outcomes, not two.** A task missing `modifiedSince` can be rejected *and
reported*, because there is a `taskId` to report it against. A task missing `taskId` or `taskType`
cannot be reported at all — the platform answers 400 to any result lacking either — so the only
honest thing to do is log it and move on. Collapsing these into one "invalid" would mean either
posting results the platform rejects, or silently dropping tasks that could have been reported.

**`GetOrders` caps orders, not line items.** A CTE selects `TOP (@MaxRecords)` headers and only then
joins details, so the cap can never slice an order in half and post a partial basket. `recordCount`
therefore counts orders: reporting 40,370 for 12,304 synced orders would misdescribe what was synced.

### Four facts about AdventureWorks that a naive join gets wrong

Each was established by querying the database, and each has an integration test that fails if the
join is written the obvious way.

| Fact | Consequence |
|---|---|
| 24 customers have more than one address | A plain join duplicates those customers. `OUTER APPLY` collapses the fan-out to one address, preferring Home then lowest `AddressID` for determinism. |
| 701 of 19,820 customers are stores with `PersonID IS NULL` | `INNER JOIN Person.Person` drops them silently. Every person-derived column is `LEFT JOIN`ed and nullable. |
| 209 of 504 products have `ProductSubcategoryID IS NULL` | `INNER JOIN` to category drops 41% of the catalogue and still looks like it worked. |
| `ProductInventory`'s key is (`ProductID`, `LocationID`) | `productId` repeats in the result. That is the contract, not a bug to dedupe away. |

Email and phone *are* 1:1 per customer (verified: no customer has two), so those stay plain
`LEFT JOIN`s with no dedup.

**Two type problems the sample payloads hide.** `Status`, `Bin`, `Quantity` and `OrderQty` are
`tinyint`/`smallint` but appear as plain numbers in the samples; they are `CAST` to `int` in SQL so
the DTO and wire types agree by construction. And SQL Server's `datetime` is timezone-naive, so
Dapper returns `DateTimeKind.Unspecified` and `System.Text.Json` would emit
`"2025-02-07T10:01:36.827"` with no offset — every sample payload shows a trailing `Z`.
`UtcDateTimeJsonConverter` applies the rule "a naive database timestamp is UTC" in one place. A
`Local` value is converted, not relabelled, so the instant never moves.

---

## Security Measures

| Measure | Implementation |
|---|---|
| **SQL injection** | `modifiedSince` is the only attacker-controlled value that reaches SQL. Every statement in `SyncQueries` is a compile-time constant; nothing is composed, formatted or interpolated at run time. `@ModifiedSince` and `@MaxRecords` bind as `DbParameter`s. By the time a value reaches that layer it is a `DateTime`, so an injection payload cannot even be *expressed*. |
| **Strict input parsing** | The validator uses `DateTimeOffset.TryParseExact` against a closed list of ISO-8601 formats, not the permissive culture-sensitive `TryParse`, which accepts `"1/1/2025"` and `"January 1"`. Parameter binding already defeats injection; this is the second layer, and a narrow accepted surface is easier to reason about than a wide one. |
| **No dynamic SQL from `taskType`** | `taskType` is only ever a dictionary key. It never becomes a table name, a column name or any fragment of SQL. An unrecognised type is a `failed` result and no query runs. |
| **Unbounded result sets** | `MaxRecords` (default 5000, validated 1–100,000). Uncapped, `GetOrders` returns 12,304 orders and 40,370 detail lines, and a multi-megabyte POST body. |
| **Secret handling** | The API key comes from configuration (env var / user-secrets), never from source, and is never logged — the header *name* is logged, never its value. A test asserts the key does not appear in any log entry, and the end-to-end run confirms zero occurrences in the agent's output. |
| **Transport pinning** | `HttpClient.BaseAddress` is pinned to the configured origin and the agent never follows a server-supplied URL. `PlatformOptions` implements `IValidatableObject` to reject a `BaseAddress` that is not an absolute `http(s)` URI, so it cannot be pointed at `file://`. |
| **Error sanitisation** | Provider exception text names the server, instance, database and sometimes the user. `ErrorSanitizer` maps an exception to a category; only that category crosses the wire. The full exception goes to the *local* log, where the machine's operator can already see everything. The rejected `modifiedSince` value is never echoed back either, since it is attacker-controlled and the platform renders what it is given. |
| **Least privilege** | The connection string sets `ApplicationIntent=ReadOnly`, `Encrypt=True` and an explicit `TrustServerCertificate`. A production deployment should use a login granted only `db_datareader`. |
| **Timeouts and cancellation** | `CommandTimeout` on every query, `HttpClient.Timeout`, and a `CancellationToken` threaded end-to-end into `CommandDefinition` so shutdown actually cancels an in-flight query. |
| **Resilience** | `AddStandardResilienceHandler` (retry with exponential backoff and jitter, concurrency limiter, circuit breaker). Without the breaker, a platform outage turns every poll into a retry storm. A `204` sleeps one poll interval; never a hot loop. |

---

## Testing Strategy

**169 unit tests, 16 integration tests, 0 warnings.** Every commit ships production code together
with the tests for that code — there was no "tests come later" phase.

Coverage is 100% of lines on `Sync/`, `Validation/`, `Platform/` and `Worker.cs`. Excluded by design:
`Program.cs` (DI wiring, proven by running the agent), the SQL constants, `SqlQueryExecutor` (the
ADO.NET seam itself), and the DTOs. Those exclusions are the reason the integration tests exist.

What the tests actually pin down, beyond line coverage:

- **The loop.** `FakeTimeProvider` means "waits one poll interval" is asserted by *refusing* to
  advance the clock and checking no second poll happens. A regression turning the idle path into a
  hot loop fails the test rather than merely running faster. The whole suite runs in under 200ms.
- **Shutdown mid-poll vs mid-delay** are different code paths; both are covered, and the mid-poll
  one asserts the cancellation is *not* logged as an error.
- **The contract.** `ContractShapeTests` copies `docs/sample-payloads/result-*.json` into the test
  output and diffs each DTO's serialised field names against them. Renaming `Subcategory` to
  `SubCategory` fails it. Field *names* only, never values — see Known Limitations.
- **The cap can't truncate an order.** The integration test caps at five orders, then asks the
  database directly how many detail lines each of those five has, and asserts the posted order
  carries all of them.
- **Serialisation of `IReadOnlyList<object>`.** `System.Text.Json` only writes an element's real
  properties when the declared element type is *exactly* `object`; anything narrower and every row
  silently serialises as `{}`. The four-handlers-one-dispatcher design rests on that, so it is
  pinned by a test rather than discovered against the live platform.

Two tests were deliberately *not* written. There is no "injection string is rejected" integration
test, because `SqlQueryParameters.ModifiedSince` is a `DateTime` and the payload cannot be expressed
at that layer — such a test would pass regardless of what the SQL did, which is worse than no test.
The hostile strings are rejected in `TaskValidatorTests`, at the boundary where they can arrive.
Similarly there is no "missing `BaseAddress` is rejected" test: the property has a non-null default,
so a `[Required]` on it could never fail, and the annotation was replaced with a rule that can.

**End-to-end.** The agent was run against the real platform app with all four buttons clicked.
Every task returned `accepted: true` with a non-zero `recordCount`:

| Task | recordCount | Note |
|---|---|---|
| `GetCustomers` | 5000 | capped (19,820 match) |
| `GetProducts` | 504 | all |
| `GetOrders` | 5000 | capped (12,304 match) |
| `GetProductInventory` | 609 | all matching |

The API key appeared zero times in the agent's log.

**With more time:** a test that runs the real host with `WebApplicationFactory`-style substitution
to cover `Program.cs` wiring; property-based tests over `OrderResultBuilder`; and a load test to
find where `MaxRecords` should actually sit rather than defaulting it to 5000.

---

## Known Limitations

1. **Store customers lose their name.** `result-get-customers.json` has `firstName`/`lastName` but
   no company/store field, so the 701 store-only customers are emitted with both null. The
   store-name fallback has nowhere to go in the customer contract, and I did not invent a
   `storeName` field. It therefore applies only to `GetOrders.customerName`, which is a single
   combined field. This is a gap in the contract, not in the query.

2. **`Customer.ModifiedDate` is identical for all 19,820 rows** (a restore artefact), so
   `modifiedSince` does not actually filter customers incrementally. A more faithful
   implementation would filter on the greatest of `Customer`, `Person` and `Address`
   `ModifiedDate` — but `GREATEST` needs SQL Server 2022+, and the real question ("what does
   *modified* mean for a record assembled from four tables?") deserves a decision, not a guess.
   I filter on `Customer.ModifiedDate` and say so.

3. **The sample payloads are shape examples, not reproducible output.** `result-get-orders.json`
   shows order 43659, whose `ModifiedDate` is 2022-06-06 — a `modifiedSince` of 2025-01-01 excludes
   it. So the tests match the payloads field-name-by-field-name and never value-by-value.

4. **No pagination cursor.** The API contract has no way to say "give me the next 5000", so a client
   with more than `MaxRecords` changed rows silently syncs only the first page, ordered by primary
   key. The cap is a safety measure, not a paging strategy. A real agent would need a cursor
   (`lastSyncedId` + `modifiedSince`) in the task contract.

5. **`recordCount` for `GetOrders` counts orders, not line items.** That matches what `MaxRecords`
   caps, and is the honest reading, but the contract does not say which is meant.

6. **The agent does not persist anything.** A task dequeued and then lost to a crash before its
   result is posted is simply never reported. The platform has no re-delivery mechanism to lean on.

---

## AI Tools Used

Claude Code (Opus 4.8), used throughout and heavily. Specifically:

- **Schema exploration.** Ran `sqlcmd` queries against `AdventureWorks2025` to establish the row
  counts, the multi-address fan-out, the null-subcategory and null-person cases, and the real column
  types. Every "fact" in this document was verified against the database rather than recalled — the
  `tinyint`/`smallint` discovery came from querying `sys.columns`, not from reading the samples.
- **Writing the production code and the tests**, as a sequence of commits, each gated on build
  warnings, a real test count, and a per-directory coverage floor.
- **Reviewing its own output.** This mattered more than the generation. It caught, among others:
  a `[Required]` annotation on a defaulted property that could never fail; a `record` whose
  public `init` setters let `with` bypass the factories the doc said were authoritative; a
  `coverlet.runsettings` that excluded `CompilerGeneratedAttribute` and so reported 100% coverage of
  `PlatformClient` while measuring 11 of its 72 lines; and a `TestCaseFilter` in runsettings that is
  ANDed with `--filter`, which meant `--filter Category=Integration` matched **zero tests and exited
  green** — a passing integration suite that never ran.

I directed the design (Worker Service, Dapper, the cap, the three-outcome validator) and made the
judgement calls on trade-offs; Claude did the schema archaeology, wrote the code and tests, and
found the mistakes listed above. The `datetime`-has-no-`Z` bug in particular would have shipped and
looked correct in a manual eyeball of the sample payloads.

---

## Time Spent

About 2 hours, in line with the estimate.

| Phase | Time |
|---|---|
| Installing SQL Server Express and SSMS, restoring `AdventureWorks2025` | 20 min |
| Reading the brief, exploring the schema, verifying the data facts | 20 min |
| Implementation (6 commits, code + tests together) | ~60 min |
| End-to-end verification, write-up | 20 min |

A sixth of the budget went on environment setup before a line of code was written, which the
2-hour estimate does not appear to account for. Of the rest, writing the four queries was quick;
being sure they are *correct* — that the address join does not duplicate customers, that the
category join does not drop 209 products — is where the time went, and is the part that would have
caused a production incident had I skipped it.

---

## Feedback

The challenge is well-built, and the schema exploration is genuinely the interesting part. Three
observations:

1. **The sample payloads cannot be reproduced from the database.** `result-get-orders.json` shows
   order 43659 with `modifiedSince: 2025-01-01`, but that order's `ModifiedDate` is 2022-06-06. A
   candidate who trusts the samples will spend a long time trying to make that row appear. Either
   lower the sample's `modifiedSince`, or state in `docs/` that the payloads illustrate shape only.
   (Commit `6434fb7`, "modify sample payload dates to receive data", suggests this was noticed.)

2. **`result-get-customers.json` has no field for a store's name**, yet 701 customers are stores with
   no person. Every candidate must either invent a field or emit two nulls. If that ambiguity is
   intentional — a test of whether the candidate notices and documents it — it works well. If not,
   adding `companyName` would remove the guesswork.

3. **The 2-hour estimate does not seem to budget for environment setup.** Installing SQL Server
   Express and SSMS and restoring `AdventureWorks2025` took 20 minutes — a sixth of the budget —
   before any code was written. Worth either calling out in the prerequisites, or excluding from
   the estimate explicitly.

Minor: `docs/api-contract.md` documents `parameters` as an object, and the platform models it as
`Dictionary<string, string>`. Worth stating that all values are strings — it changes how the
`modifiedSince` parse is written.

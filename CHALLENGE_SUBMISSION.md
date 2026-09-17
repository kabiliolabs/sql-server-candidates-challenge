# Challenge Submission

## Candidate

- **Name:** Federico Etcheverry
- **Date:** 2026-09-17

---

## How to Run

1. **Prerequisites:** .NET 8 SDK, SQL Server (2025 Developer/Express — the
   provided `AdventureWorks2025.bak` is SQL Server 2025 format and will not
   restore on 2022 or earlier, error "version 998 vs 958"), the `AdventureWorks2025`
   database restored and reachable.

2. **Restore the database** (adjust paths/instance name to your setup):
   ```sql
   USE [master];
   RESTORE DATABASE [AdventureWorks2025]
   FROM DISK = N'<path to>\AdventureWorks2025.bak'
   WITH
     MOVE 'AdventureWorks'     TO N'<data path>\AdventureWorks2025.mdf',
     MOVE 'AdventureWorks_log' TO N'<data path>\AdventureWorks2025_log.ldf',
     STATS = 25;
   ```
   Verify: `SELECT COUNT(*) FROM Sales.Customer;` should return 19,820.

3. **Set environment variables** (never in config files):
   ```
   SYNCAGENT_DB_CONNECTION = Server=<host>\<instance>;Database=AdventureWorks2025;Integrated Security=true;TrustServerCertificate=true;
   SYNCAGENT_API_KEY       = candidate-test-key-2026
   ```
   Optionally `SYNCAGENT_PLATFORM_URL` to override the default
   `http://localhost:5100/` (see `candidate/src/SyncAgent.Worker/appsettings.json`
   for every other tunable — row cap, poll interval, resilience settings — all
   non-secret and safe to edit there).

4. **Run the test platform:**
   ```
   cd src/SyncPlatform
   dotnet run --project SyncPlatform
   ```

5. **Run the sync agent:**
   ```
   cd candidate/src/SyncAgent.Worker
   dotnet run
   ```
   Click any task button in the SyncPlatform window; the agent will pick it up
   within one poll interval (5s by default), execute it, and post the result. Logs
   go to the console and to `logs/syncagent.log` (relative to the process's
   working directory).

6. **Run the tests:**
   ```
   cd candidate
   dotnet test
   ```
   17 tests, all pass with no database available (SQL integration tests
   auto-skip). To also run those against a real database:
   ```
   set SYNCAGENT_TEST_DB_CONNECTION=<same connection string as above>
   dotnet test
   ```

---

## Architecture Decisions

**Layering:** `SyncAgent.Core` (no I/O dependencies) / `SyncAgent.Infrastructure`
(Dapper, HTTP, log4net) / `SyncAgent.Worker` (the `BackgroundService` host and
composition root). Core has zero I/O so it's portable to .NET Framework 4.8 if a
client's on-prem runtime doesn't support .NET 8 — not built out in this
submission, but the design holds for it.

**Dispatch:** each task type is a [Mediator](https://github.com/martinothamar/Mediator)
`IRequest<IReadOnlyList<object>>` query with a matching handler. I chose
Mediator (martinothamar) over MediatR specifically because MediatR's recent major
versions require a commercial license above a revenue threshold — not something
I wanted to introduce into a company's codebase without an explicit conversation
about it. A `LoggingPipelineBehavior<TRequest, TResponse>` wraps every request
with duration/record-count logging, so that concern lives in one place instead of
inside each handler. Because the platform's `taskType` arrives as an untyped
runtime string but Mediator dispatch is compile-time typed, `SyncTaskDispatcher`
still keeps a small string→query map — the one place adding a task type costs
more than "one class, one DI line."

**Repositories:** `ISyncQueryRepository<TRecord>` is one generic interface closed
over each task's record type, implemented with Dapper in Infrastructure. Handlers
in Core depend only on this interface, never on Dapper directly — Core stays free
of I/O dependencies. I considered EF Core here and went with Dapper: this
challenge is read-only, SQL-only extraction with shapes that don't map 1:1 to
tables (a customer row assembled from six joined tables; an order's header and
detail lines fetched and assembled separately), and EF's core value —
change-tracking, migrations — isn't used for that. Dapper also keeps the SQL
visible, which is part of demonstrating I understood the schema rather than
letting an ORM hide it.

**Adding a new task type** means: one record type (if the shape is new), one
query + repository implementation, one handler class, one line in
`SyncTaskDispatcher`'s map, and one DI registration line. Handlers are
deliberately four separate small classes rather than one generic
`SyncTaskHandler<TRecord>` — they're currently identical in shape, but that's a
coincidence of the first four task types, not a guarantee; extracting an
abstraction after only one real pattern felt premature, and a 5th task type is
free to diverge (extra validation, a different response shape) without fighting a
shared base.

**Never throws past the dispatcher:** the platform simulator has no requeue —
GET /next-task removes a task from its queue via a plain `ConcurrentQueue`,
verified by reading `TaskQueueService.cs`, with no lease/ack/timeout mechanism, so
a task dequeued and never posted back is lost for good. `SyncTaskDispatcher`
handles the two *expected* failure modes (unrecognized `taskType`, missing/
malformed `modifiedSince`) with plain checks that return a `"failed"` result, and
catches anything genuinely unexpected (a repository call throwing) once,
centrally — either way, the worker loop can always post something back to the
platform.

**Options pattern with fail-fast validation:** four options types
(`SyncAgentOptions`, `SqlOptions`, `PlatformOptions`, `ResilienceOptions`), each
with an `IValidateOptions<T>` and `.ValidateOnStart()`. `Program.cs` also
explicitly resolves every options type right after building the host, logging
each validation failure individually via `ILogger` at Critical before exiting
non-zero — a missing `SYNCAGENT_DB_CONNECTION` produces one clear log line, not a
stack trace on the first task.

**A real correction found during implementation, not just planning:** my own
pre-code research (done the evening before, verifying against a real database)
concluded `GetCustomers` should return 19,820 rows, explicitly dismissing another
candidate's reported 19,119 as a mistake. Re-verifying against the full table
while writing the actual query found the real cause: 701 of those 19,820 rows are
store/B2B accounts with `PersonID IS NULL` (no linked `Person` row — no name,
email, or address). An inner join to get person data correctly excludes them,
landing at 19,119 — the other candidate's number was right, and I was wrong to
dismiss it based on a 2-row spot check. I've excluded store accounts from
`GetCustomers` since the sample payload has no shape for a business account.

---

## Security Measures

- **Secrets never in source or config files.** `SYNCAGENT_DB_CONNECTION` and
  `SYNCAGENT_API_KEY` are read from environment variables only; everything else
  (row cap, poll interval, resilience tuning, platform URL) is non-secret and
  lives in `appsettings.json`.
- **100% parameterized SQL** — `@modifiedSince`, `@Top`, `@SalesOrderIds` — no
  string concatenation anywhere.
- **`taskType` allow-listed** against the four known values before it ever
  touches a query or a type lookup; anything else fails cleanly as a `"failed"`
  result.
- **`TOP(@n)` row cap** (default 5,000, configurable) plus a SQL command timeout,
  as defenses against a hostile or malformed task payload — not real pagination
  (see Known Limitations).
- **HTTP resilience toward the platform:** a self-throttling rate limiter
  (configurable max requests/sec, so a misconfigured poll interval or a retry
  storm can't hammer the platform), retry with exponential backoff, a circuit
  breaker, and a timeout — via `Microsoft.Extensions.Http.Resilience`. The rate
  limiter is explicitly disposed on pipeline teardown (`OnPipelineDisposed`) to
  avoid leaking its internal timer across `HttpClientFactory`'s periodic handler
  rotation, a real issue an automated code review caught during this build.
- **No PII in logs, no secrets in logs.** log4net is the sole logging sink, and
  every log call in the codebase passes only metadata — task type, `taskId`,
  record counts, durations, HTTP status codes, exception messages — never row
  data, the connection string, or the API key, at any log level including Debug.
  This is enforced by convention/code review right now, not an automated test —
  see Known Limitations.

---

## Testing Strategy

17 tests across three projects, `dotnet test` green with **no database required**:

- **`SyncAgent.Core.Tests` (6):** `SyncTaskDispatcher` behavior — unknown task
  type and malformed/missing `modifiedSince` both fail without throwing; a
  handler exception becomes a `"failed"` result carrying the exception's message;
  a successful dispatch returns `"completed"` with a matching `recordCount`;
  multiple registered handlers resolve to the correct one. Built on a *real*
  Mediator pipeline (a throwaway `ServiceCollection` + `AddMediator()`), not a
  hand-rolled fake `ISender`, so the test exercises the same handler-resolution
  path production uses.
- **`SyncAgent.Infrastructure.Tests` (8):**
  - 4 **contract tests** comparing each record type's serialized JSON shape
    (property names, camelCase) against `docs/sample-payloads/result-*.json` —
    structure only, not literal values, since the real database won't match the
    docs' example rows. All four passed on the first run.
  - 4 **SQL integration tests** against the real database, gated by
    `SYNCAGENT_TEST_DB_CONNECTION` via a custom `[SkippableFact]` so they report
    as *skipped*, not failed, when no database is configured. Includes a
    regression guard pinning `GetProductInventory` to the documented-correct
    609-row count rather than the 1,069-row trap (filtering by the wrong table's
    `ModifiedDate`).
- **`SyncAgent.Worker.Tests` (3):** the polling loop against a fake
  `ISyncPlatformClient`, driven by a `FakeTimeProvider` so poll cycles are
  triggered deterministically by advancing virtual time rather than waiting on
  real delays. Covers: the platform failing on every poll never crashes the loop;
  a `204` never posts a result; an unrecognized task type still posts exactly one
  `"failed"` result.

**What I'd add with more time:** an automated test asserting no logger call in
the codebase is ever passed a full row/record object (currently just verified by
reading the code); load/soak testing of the resilience pipeline under an actual
platform outage rather than the manual verification I did.

**How I verified beyond unit tests:** every task type was also manually exercised
against the real restored database and the actual running `SyncPlatform` WPF app
(not just mocked) — twice, once for the MVP and once after adding
Mediator/log4net/resilience — confirming the full round trip end-to-end, not just
each piece in isolation. This caught two real bugs unit tests alone wouldn't have:
Dapper can't materialize a `DateTimeOffset` constructor parameter from a SQL
`datetime` column (fails with a misleading "needs a parameterless constructor"
error), and `GetOrders`' detail query hit SQL Server's 2,100-parameter limit once
enough orders came back to expand `IN @Ids` past it. Both are fixed and covered by
the integration/contract tests now.

I also ran an automated multi-agent code review (Claude Code's `/code-review
ultra`) against the finished robustness-pass commit, which caught a real resource
leak (the rate limiter above) and a flaky-test issue (the wall-clock-based Worker
tests) — both fixed.

---

## Known Limitations

- **No real pagination.** `TOP(@n)` caps large result sets (~19.1k customers,
  ~12.3k orders); a production sync against a growing dataset would need
  cursor-based paging, which the current API contract has no parameter for.
- **No requeue/idempotency beyond what the simulator itself does** (which is
  none). A task dequeued and never posted back — e.g. the agent process crashing
  mid-execution — is lost. A real system would need ack/lease semantics on the
  platform side.
- **`phone` is always `null`** in `GetCustomers` — no field in AdventureWorks
  matches the sample payload's phone format. I considered joining
  `Person.PhoneNumber` but decided against it: it's a different kind of phone
  number (personal, not necessarily a business contact number) than what a synced
  "customer contact" would usually mean, and it's not populated for every person
  either — `null` with this explanation felt more honest than a guess.
- **Store/B2B customer accounts are excluded from `GetCustomers`** — see the
  Architecture Decisions section for why, and why my own initial research got
  this wrong.
- **.NET 8 only.** Core has no I/O dependencies and the design is portable to
  .NET Framework 4.8 for Kabilio's actual on-prem client runtimes, but that
  portability isn't built or verified in this submission — a deliberate scope
  decision to spend the available time on the four task types and their
  correctness instead.
- **No automated PII-policy guard test** — the "never log row data" rule is
  enforced by code review convention, not a test that would fail if violated.

---

## AI Tools Used

I used **Claude Code** (Sonnet 5) throughout, in an interactive pairing style —
not "generate the whole thing and review at the end." Specifics:

- **Planning review:** the evening before, I used a separate AI research session
  to draft an initial plan (schema exploration, verified row counts, design
  decisions) against a practice database. I asked Claude Code to review that plan
  for internal consistency against the actual API contract and sample payloads
  before writing any code — it cross-checked the claims, confirmed several of
  them directly against the sample data, and flagged genuinely open trade-offs
  (the `phone` field, .NET targeting scope, the row-cap-vs-`recordCount` contract
  tension) for me to decide rather than silently picking an answer.
- **Environment setup:** Claude Code diagnosed and fixed a SQL Server install
  failure (a locale mismatch between the installer's default media and Windows'
  display language) that I would have spent real time on alone.
- **Implementation:** wrote essentially all of the code, but I made every
  non-obvious design call myself when asked (Mediator vs. MediatR vs. hand-rolled;
  the phone-field decision; row-cap vs. pagination trade-off; the store-account
  exclusion once the bug was found) — Claude Code surfaced the decision and its
  trade-offs rather than picking silently.
- **Bug-finding through actual execution, not just code generation:** Claude Code
  cannot click a GUI button, so verifying the full poll→query→post round trip
  required me to run the real `SyncPlatform` WPF app and click each task-type
  button myself while it watched the agent's live output. This is how the
  `DateTimeOffset`/Dapper bug and the SQL Server parameter-limit bug were actually
  found — a code read-through wouldn't have caught either.
- **Found and corrected an error in my own prior research**, not just
  implemented it: the 19,820-vs-19,119 customer count discrepancy (see
  Architecture Decisions).
- **Code review:** ran Claude Code's `/code-review ultra` (a multi-agent
  automated review) against the finished code, which caught a real resource leak
  and a flaky test that a solo pass likely would have missed.
- **This document** was drafted by Claude Code from the session's own running
  log (`AI_USAGE_LOG.md`, committed alongside the code) and reviewed/edited by
  me before submitting.

The full, running, timestamped account of every AI interaction — not just this
summary — is in [`AI_USAGE_LOG.md`](AI_USAGE_LOG.md), kept updated across the
session rather than reconstructed afterward.

---

## Time Spent

Roughly 4-5 hours across one day, against the README's ~2h estimate and the
4-day submission window:

- Environment setup (SQL Server 2025 install/troubleshooting, DB restore): ~45 min
- Planning review and technology decisions: ~30 min
- MVP implementation (all four task types, working end-to-end): ~1.5h, including
  live debugging against the real simulator
- Robustness pass (Mediator, log4net, HTTP resilience, test suite): ~1.5h
- Code review and fixes: ~20 min
- This document: ~20 min

I chose to exceed the 2h estimate deliberately, given the 4-day window and the
value of verifying against a real running simulator rather than stopping at
"the code looks right."

---

## Feedback

- The README's `RESTORE DATABASE` T-SQL uses logical file names
  (`AdventureWorks2025`, `AdventureWorks2025_log`) that don't match the actual
  backup — the real ones are `AdventureWorks` and `AdventureWorks_log`, only
  discoverable via `RESTORE FILELISTONLY`. Worth fixing in the README directly.
- The `.bak` being SQL Server 2025-only isn't mentioned anywhere in the README,
  and the failure mode (a cryptic "version 998 vs 958" error) doesn't point at
  the cause. A one-line note would save whoever hits this real setup time.
- Requiring GUI interaction with a WPF app to enqueue tasks makes full
  end-to-end verification awkward for anyone pairing with an AI tool that can't
  drive a desktop UI — an HTTP endpoint to enqueue tasks (even a debug-only one)
  would make the whole loop scriptable/automatable while keeping the polling
  contract identical.
- The `phone` field in the sample payload having no corresponding schema field
  felt like a deliberate trap once I found it, and if so, it's a good one — it
  forces a real decision instead of a mechanical field mapping. Worth confirming
  in a debrief either way.
- Overall a well-scoped challenge: the four task types cover genuinely different
  query shapes (simple filter, multi-join with fan-out risk, header+detail
  assembly, straightforward join), which is enough to exercise real design
  decisions without being a slog.

# Challenge Submission

## Candidate

- **Name:** JC
- **Date:** 2026-09-12

---

## How to Run

**Requirements:** .NET 10 SDK (pinned to 10.0.401), SQL Server with `AdventureWorks2025` restored, SyncPlatform running on `http://localhost:5100`.

```bash
# 1. Start SyncPlatform
cd src/SyncPlatform
dotnet run --project SyncPlatform

# 2. Build and run SyncAgent
cd ../candidate
dotnet restore
dotnet build
dotnet run --project src/SyncAgent.Worker
```

Then click any task button in SyncPlatform. The agent picks it up on the next poll cycle (10s default), queries AdventureWorks, and posts the result back.

```bash
# Run all tests
dotnet test

# Run unit tests only (no DB required)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"
```

Configuration (`appsettings.json`) can be overridden via environment variables, e.g. `SyncPlatform__BaseUrl`, `SyncPlatform__ApiKey`, `Database__ConnectionString`, `Polling__IntervalSeconds`.

---

## Architecture Decisions

Clean Architecture, dependencies pointing inward only: `Domain` (no dependencies) ← `Application` ← `Infrastructure` ← `Worker`.

- **Handlers over a switch.** `SyncTaskDispatcher` resolves the matching `ISyncTaskHandler` from everything DI provides. Adding a new task type means one new handler class plus one DI registration line — nothing else changes (dispatcher, poller, Worker stay untouched).
- **DI lifetimes are deliberate.** `SyncAgentWorker` is a Singleton (one per host); `SyncTaskPoller` and the repositories are Scoped, created fresh per poll cycle, matching how `SqlConnection` should be used with pooling; `ISyncPlatformConnectivityTracker` is a Singleton because its state (has the platform been down before) must survive across cycles — as Scoped, every failure would log as a "new" outage instead of a continuing one.
- **Worker Service over alternatives.** A console app has no lifecycle management; a legacy Windows Service is boilerplate-heavy. `BackgroundService` gives native async polling, `CancellationToken`-based graceful shutdown, and full DI/logging, and `AddWindowsService()` turns it into an installable Windows Service with no extra code.
- **Contract records are explicit.** Each task type has its own record with `[JsonPropertyName]` on every field, diffable directly against `docs/sample-payloads/` — necessary because field naming is inconsistent across task types (`name` vs `productName` for the same kind of data).

---

## Security Measures

- **SQL injection** — every query is a `const string` with a single `@ModifiedSince` parameter added with an explicit `SqlDbType`. No value from the platform is ever concatenated into SQL.
- **Untrusted input** — `modifiedSince` is validated against an exact ISO-8601 UTC format before it reaches the database. Missing or malformed values become a `failed` result, not an exception.
- **Nothing internal leaks to the platform** — only `exception.Message` travels in `errorMessage`; stack traces stay in the local log.
- **No secrets in the repository** — the database uses Windows authentication, so there's no password to store. The API key is the challenge's published test key; in production it would come from environment variables or a secret store, which the existing configuration already supports.
- **Nothing sensitive is logged** — no connection strings, no API key values.
- **Bounded waits** — the HTTP client and SQL commands both have configurable timeouts, so a hung dependency can't stall the agent indefinitely.
- **Fail fast on bad configuration** — options are validated at startup, so a malformed URL stops the host immediately with a named setting instead of failing on every poll forever.

---

## Testing Strategy

50 tests across five projects — four run anywhere, one needs the database:

| Project                           | Tests | Covers                                                                                                                                  |
| --------------------------------- | ----- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `Domain.Tests`                    | 11    | `modifiedSince` parsing and rejection of malformed/offset/impossible dates.                                                             |
| `Application.Tests`               | 14    | Handler mapping, validation short-circuiting, cancellation vs. failure, dispatcher routing, poller's success/failure/unreachable paths. |
| `Infrastructure.Tests`            | 14    | HTTP client against a fake handler, result envelope shape, DI composition validation.                                                   |
| `Infrastructure.IntegrationTests` | 10    | Real queries against AdventureWorks2025, filter behavior, end-to-end handler+repository output checked against the contract.            |
| `Worker.Tests`                    | 1     | Hosted service starts and stops without throwing.                                                                                       |

Beyond the suite, I ran the compiled Worker against a stand-in platform over real HTTP to verify the whole chain end to end for all four task types, plus failure paths (unknown task type, malformed parameters, platform unreachable, invalid config at startup).

**With more time:** a containerized SQL Server so integration tests run anywhere; a test that starts both real applications together; coverage of a mid-task shutdown.

---

## Known Limitations

- **The buttons return zero records.** All AdventureWorks `ModifiedDate` values are ≤ 2014 and the platform sends `modifiedSince = 2025-01-01`. I verified the queries against earlier cut-offs instead of changing the filter's meaning to make the demo look better.
- **The integration tests need SQL Server** at `localhost\SQLEXPRESS` with `AdventureWorks2025`, in a separate project so the rest of the suite runs anywhere.
- **No pagination.** The contract defines none, so a result is sent as one payload (`GetOrders` over full history is ~19 MB, ~5s).
- **One task per cycle**, since the endpoint hands out a single task per request and says nothing about parallelism.
- **No retry or backoff.** A failed poll is logged and retried on the next tick; the contract doesn't define retry semantics.
- **A task isn't re-sent if posting the result fails** — the contract has no acknowledgement/redelivery mechanism.
- **Windows Service installation wasn't executed.** `AddWindowsService` is wired and the app runs as a console host; I didn't install it under the SCM.

---

## AI Tools Used

Claude (Claude Code) was used throughout, driven in phases rather than by asking for a finished solution.

- **Discovery** — had it read `docs/api-contract.md`, every sample payload, and the SyncPlatform source itself (`HttpSyncServer`, `TaskQueueService`, the models), so the implementation matched real behavior rather than documentation alone. That's how the inconsistent field naming and the exact-match API key check were found.
- **Schema exploration** — queried the live AdventureWorks database with `sqlcmd` to confirm tables, nullability and cardinality instead of assuming.
- **Implementation** — the Clean Architecture layout, handlers, repositories and tests were written with Claude, reviewing each piece against the contract.
- **Audit** — had it re-audit the whole codebase as a reviewer and fix what it found; several real defects (DI lifetime mismatch, a timezone bug in date parsing, duplicated headers, missing config validation, among others) came out of that pass.

---

## Time Spent

Approximately 3 hours:

- 20 minutes on discovery and reading the platform's source
- 30 minutes on schema exploration
- 1'5 hours on implementation
- 30 minutes on review passes and the defects they surfaced, and the remainder on tests and documentation.

---

## Feedback

The challenge is well built. Shipping a working SyncPlatform rather than only a written contract made a real difference — reading `HttpSyncServer` is what revealed the exact-match API key check, which a documentation-only contract wouldn't have shown.

Two things worth flagging:

- **The `modifiedSince` value cannot match any data.** The samples and `TaskQueueService` send `2025-01-01`, while AdventureWorks ships nothing newer than 2014, so a correct implementation looks broken when the buttons are clicked. A date before 2014, or a note in the README, would remove the ambiguity.
- **The sample payloads are aspirational in places.** `result-get-orders.json` shows three line items for order 43659 where the database has twelve, and the dates don't match the real rows. Stating that they're illustrative would save a candidate from trying to reproduce them exactly.

The inconsistent field naming between task types (`name` vs `productName`) is realistic and a good detail — it rewards reading the payloads carefully.

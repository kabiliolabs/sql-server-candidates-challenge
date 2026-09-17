# AI Usage Log

This is a running, honest log of how AI (Claude Code) was used while building this
submission, kept alongside the work as it happened rather than reconstructed at the
end. It complements the "AI usage" section of `CHALLENGE_SUBMISSION.md` — that file
should summarize; this one keeps the detail.

For each session: what was asked, what the AI did, and what I did myself.

---

## 2026-09-17 — Environment readiness check

**Asked:** Analyze the plan in `planning/` and check whether the local machine is
ready to start the challenge.

**AI did:**
- Read `CHALLENGE_DESCRIPTION.md`, `SETUP_WINDOWS.md`, and `CHEATSHEET.md` to summarize
  the task and prerequisites.
- Checked installed tooling: `dotnet --list-sdks`, `git --version`, `sqlcmd` version,
  and the state of the `MSSQLSERVER` Windows service.
- Found: .NET 8 and .NET 10 SDKs present; Git present; challenge repo and
  `AdventureWorks2025.bak` present.
- Found blockers: only SQL Server **2019** (`MSSQL15`) is installed, but the provided
  `.bak` is SQL Server **2025** format (per prior Ubuntu verification in
  `SETUP_WINDOWS.md`, this format mismatch throws "version 998 vs 958" on 2022, so
  it will also fail on 2019) — SQL Server 2025 needs to be installed. Also the
  `MSSQLSERVER` service was stopped and could not be started from a non-elevated shell.

**I did:** Reviewed the findings, confirmed the plan, decided to fix SQL Server
version before proceeding.

---

## 2026-09-17 — Repo setup

**Asked:** Create this AI usage log, and initialize git in the challenge project so
work is tracked from the start.

**AI did:**
- Ran `git init` in `sql-server-candidates-challenge/` (the folder that will become
  the submission — not the parent `planning/` folder, which holds private prep
  material and research notes that shouldn't ship with the submission).
- Noticed `CHEATSHEET-local.md` (personal prep/strategy notes) was already sitting in
  this folder and added it to `.gitignore` so it isn't committed — it's not part of
  the deliverable.
- Created this file.

**I did:** Asked for the log and the git init.

---

## 2026-09-17 — SQL Server 2025 install and database restore

**Asked:** Set up the environment and install what's missing.

**AI did:**
- Checked `winget` for SQL Server 2025 packages; guided an elevated install of
  `Microsoft.SQLServer.2025.Developer` as a **named instance** (`MSSQLSERVER01`) so
  the existing SQL Server 2019 default instance was left untouched.
- Diagnosed a locale-mismatch install failure (OS is Spanish-language, bootstrapper
  grabbed non-English media) and provided the fix: re-run the SSEI bootstrapper with
  `/ENU` to force English-language media.
- After install, verified the `MSSQL$MSSQLSERVER01` service was running, confirmed
  `sqlcmd` could read `AdventureWorks2025.bak` directly from `planning/` (via
  `RESTORE FILELISTONLY`), and ran the `RESTORE DATABASE` with the corrected logical
  file names into the new instance's `DATA` folder.
- Verified row counts against the ones already validated in `SETUP_WINDOWS.md`
  (19,820 customers / 31,465 orders / 504 products all matched).
- Built the `SyncPlatform` test app (`dotnet build`) to confirm it compiles.

**I did:** Ran the elevated PowerShell installer commands myself (the AI can't
elevate privileges), reported the errors back, and confirmed each step before
moving to the next.

---

## 2026-09-17 — Repo location fix

**Asked:** The challenge repo shouldn't be nested inside `planning/` — both should
sit side by side under the top-level `Kabilio` folder.

**AI did:** Moved `sql-server-candidates-challenge/` up to `C:\Repos\Kabilio\`
(a plain filesystem move via PowerShell, since a Bash `mv` hit a permission error)
and verified the git repository, its commit, and its branch survived the move intact.

**I did:** Asked for the reorganization.

---

## 2026-09-17 — Final layout: flatten repo, nest planning under docs

**Asked:** Move the contents of `sql-server-candidates-challenge/` up into the
`Kabilio` root and delete the now-empty folder, then move `planning/` inside `docs/`.

**AI did:**
- Flattened the repo: moved every file/folder (including `.git`) from
  `sql-server-candidates-challenge/` up to `C:\Repos\Kabilio\`, so the git repo root
  is now `Kabilio` itself.
- Hit a locked-folder error removing the empty shell — a Bash shell still had its
  cwd inside it, and lingering `dotnet`/MSBuild/Razor build-server processes held
  file handles open. Changed the Bash cwd, ran `dotnet build-server shutdown`, and
  the removal then succeeded.
- Moved `planning/` (private research notes, cheatsheet, and the 48MB
  `AdventureWorks2025.bak`) into `docs/planning/`.
- Since `docs/planning/` now sits inside the git repo tree, added `docs/planning/`
  to `.gitignore` before anything could be staged — `RESEARCH-NOTES-PRIVATE.md` and
  `CHEATSHEET.md` (debrief strategy notes) must not ship in the submission, and the
  `.bak` is far too large for a repo anyway.
- Rebuilt `SyncPlatform` from the new path to confirm nothing broke.

**I did:** Asked for the reorganization.

---

## 2026-09-17 — Planning review before writing code

**Asked:** Review the prior planning work (done with another AI tool, "Hermes") for
consistency, and ask whatever questions are needed before starting the implementation.

**AI did:**
- Cross-checked `CHEATSHEET.md`, `SETUP_WINDOWS.md`, and `RESEARCH-NOTES-PRIVATE.md`
  against each other and against the real `docs/api-contract.md` and sample payloads.
  Found them internally consistent (matching row counts, join paths, and design
  decisions) and confirmed two of the prep notes' claims directly against the sample
  data: the `GetOrders` sample's `orderDate` (2022) vs. its `modifiedSince` (2025)
  confirms the filter really is on `ModifiedDate`; `phone` genuinely has no match in
  AdventureWorks.
- Identified four points the prior planning had left as open trade-offs rather than
  firm decisions, and asked the user to choose:
  1. `phone` field handling (null vs. `Person.PhoneNumber` join)
  2. Whether to actually multi-target `net8.0;net48` or just document the
     portability argument
  3. How the row-limit security measure (`TOP(@n)`) should interact with the
     `recordCount == data.Length` contract requirement
  4. Whether to set up the fork/PR git remote now or later

**I did:** Chose the recommended option on all four questions: return `null` for
phone; target net8.0 only with a written portability note; use a `TOP(@n)` cap
documented as a known limitation rather than building real pagination; hold off on
the git remote until there's code to push.

---

## 2026-09-17 — Technology stack decisions

**Asked:** User specified the desired stack: a Mediator library for handlers,
log4net for logging (careful not to log PII), circuit breaker/retry/throttling for
HTTP calls, connection string from env var, Dapper for DB mapping, and "any other
mechanism that makes this app more robust."

**AI did:** Flagged two choices that needed disambiguation before scaffolding
anything, since getting them wrong would mean redoing work later:
- "Mediator" is ambiguous between MediatR (whose recent major versions require a
  commercial license above a revenue threshold — a real concern for code written
  for an employer) and the unrelated `Mediator` package by martinothamar
  (source-generator based, MIT licensed). Asked which was meant, and also offered a
  zero-dependency hand-rolled option.
- For HTTP resilience, asked whether to use `Microsoft.Extensions.Http.Resilience`
  (the official .NET 8 wrapper around Polly v8, integrates with
  `HttpClientFactory`) or wire Polly directly.
- Asked what exactly "PII" should exclude from log4net logs (metadata-only vs.
  allowing full row data at Debug level).
- Asked what the "configurable throttling" requirement should actually rate-limit
  (outbound HTTP calls only, vs. also DB query execution).

**I did:** Chose the recommended option on all four: `Mediator` (martinothamar) for
the mediator; metadata-only logging with log4net (no row data, connection strings,
or API key at any log level); `Microsoft.Extensions.Http.Resilience` for retry/
circuit-breaker/timeout; and self-throttling on outbound platform calls only.

**Final stack:** Mediator (martinothamar) · log4net · Dapper ·
Microsoft.Extensions.Http.Resilience · env-var config with fail-fast validation ·
xUnit.

---

## 2026-09-17 — Started coding: found and corrected a bug in the prior research

**Asked:** Start implementing, focus on getting an end-to-end working version first,
add robustness afterward.

**AI did:**
- Scaffolded the `candidate/` solution: `SyncAgent.Core` (no I/O), `SyncAgent.Infrastructure`
  (Dapper/SQL), `SyncAgent.Worker` (BackgroundService), and matching test projects,
  wired into one `.sln` with correct project references.
- Added `Dapper` and `Microsoft.Data.SqlClient` to Infrastructure.
- Wrote Core DTOs (`SyncTask`, `SyncResult`, per-task-type records), the
  `ISyncTaskHandler`/`ISyncPlatformClient` abstractions, and a `SyncTaskDispatcher`
  that allow-lists task types and always produces a result (never throws past
  itself) so the worker loop can always post something back.
- Before writing the customer query, verified column names and join cardinality
  directly against the restored database rather than trusting the prep notes as-is.
  This caught a real bug in `RESEARCH-NOTES-PRIVATE.md`: the notes verified the
  customer join against only the 2 example customers (11000, 11001) and concluded
  19,820 was the correct row count, explicitly dismissing another candidate's
  reported 19,119 as wrong. Checking against the full table showed 701 of the
  19,820 filtered `Sales.Customer` rows have `PersonID IS NULL` (store/B2B accounts,
  no linked `Person` row) — an inner join to get name/email/address correctly drops
  them, landing at 19,119. The other candidate's number was right; the private
  notes' dismissal of it was not.
- Flagged this to the user with the evidence and asked how to handle store
  accounts, since the sample payload's shape has no field for them.

**I did:** Chose to exclude store/B2B accounts (inner join, 19,119 rows),
documented as out of scope since the sample payload has no shape for a business
account.

---

## 2026-09-17 — MVP implementation: architecture refinement, coding, live debugging

**Asked (across several messages):** Apply a set of specific architecture
refinements while coding (query-strategy pairing, Options pattern with
`IValidateOptions<T>`, Result-type-not-exceptions for expected failures,
decorator/pipeline behaviors for cross-cutting concerns, segregated repository
interfaces, explicit phone-null mapping) — then start coding, focused on making it
work first. Later: exclude `bin`/`obj`/`.vs` from the repo; add config validation
at startup with logging.

**AI did:**
- Scaffolded the `candidate/` solution: `SyncAgent.Core` (no I/O), `SyncAgent.Infrastructure`
  (Dapper/HTTP), `SyncAgent.Worker` (BackgroundService), matching test projects.
  `dotnet new sln` defaulted to the new `.slnx` format under the installed .NET 10
  SDK; recreated it as a classic `.sln` since the README targets .NET 8 SDK and a
  reviewer might not have `.slnx` support.
- Implemented all four task types: DTOs, `ISyncTaskHandler`/`ISyncQueryRepository<TRecord>`
  abstractions, `SyncTaskDispatcher` (allow-list + never-throws), four thin Core
  handlers, four Infrastructure repositories (Dapper + parameterized SQL), the
  typed `SyncPlatformClient`, and the DI composition root in `Program.cs` — applying
  every architecture point from the mid-turn request (see `docs/IMPLEMENTATION_PLAN.md`
  §4 and §7 for the applied design; not restated here).
- Verified `.gitignore` already excluded `bin/`/`obj/`/`.vs/` correctly — no change
  needed, confirmed via `git status`.
- Added fail-fast config validation with logging: `IValidateOptions<T>` per options
  type (`SyncAgentOptions`, `SqlOptions`, `PlatformOptions`) plus an explicit
  resolve-and-catch step in `Program.cs` that logs every validation failure via
  `ILogger` at Critical before exiting non-zero, rather than letting a missing env
  var surface as a stack trace on the first task.
- Before writing the customer query, checked join cardinality against the full
  restored database rather than trusting the pre-code research notes' 2-row
  spot-check. Found and corrected a real error in those notes: they verified only
  the 2 sample customers and concluded 19,820 was right, dismissing another
  candidate's 19,119 as wrong. The actual cause of the gap is 701 store/B2B
  customers with `PersonID IS NULL` — an inner join to get person data correctly
  excludes them, landing at 19,119. Flagged this to the user with evidence and
  asked how to handle store accounts (see the "Store customers" question, answered:
  exclude them).
- Also found ~24 customers with more than one address row (fan-out risk the 2-row
  sample check couldn't have revealed) and deduped deterministically with
  `ROW_NUMBER()`.
- Ran a live, manual end-to-end test: started the real `SyncPlatform` WPF simulator
  and the real `AdventureWorks2025` database, ran the Worker, and asked the user to
  click each of the four task buttons in the simulator's UI (something the AI
  cannot do itself — no GUI automation available) while watching the Worker's
  output. This caught two real bugs invisible to a code read-through:
  1. `GetProducts` failed — Dapper's constructor-based materialization can't
     convert a SQL `datetime` column to a `DateTimeOffset` constructor parameter;
     it fails with a message that reads as if a parameterless constructor is
     missing. Fixed by introducing an internal `*Row` type with `DateTime` for
     every repository with a date column, converted explicitly to `DateTimeOffset`
     in the mapping step — same pattern already used for the `phone` field.
  2. `GetOrders` failed — SQL Server's 2,100-parameter-per-query limit, hit by
     Dapper expanding `WHERE SalesOrderID IN @Ids` into one parameter per id when
     the header query (capped at 5,000) returned enough orders. Fixed by batching
     the detail query into chunks of 2,000 ids.
  After both fixes, re-ran the same manual test and confirmed all four task types
  complete successfully with correct record counts and payload shapes matching
  `docs/sample-payloads/`.
- Updated `docs/IMPLEMENTATION_PLAN.md` throughout to reflect the applied
  architecture, the corrected customer count, and both bugs — it's now a living
  document rather than a frozen pre-code plan.
- Removed the template placeholder `UnitTest1.cs` files from the three test
  projects (no real tests written yet — next milestone).

**I did:** Specified the architecture refinements to apply, decided to focus on a
working MVP before robustness features, and manually clicked each task-type button
in the simulator's GUI so the AI could observe the live request/response payloads
— the one step in this session the AI structurally couldn't do itself.

---

## 2026-09-17 — Robustness pass: resilience, log4net, Mediator, tests

**Asked:** "Let's get this fail safe" — proceed with the robustness work deferred
from the MVP (HTTP resilience, log4net, Mediator, tests).

**AI did:**
- Added `Microsoft.Extensions.Http.Resilience` to `SyncPlatformClient`'s pipeline:
  rate limiter (self-throttle, outermost), retry (exponential backoff + jitter),
  circuit breaker, and timeout — all via a new `ResilienceOptions` (validated,
  configurable under `SyncAgent:Platform:Resilience`). Smoke-tested live against
  the real simulator to confirm the happy path (204 responses) wasn't affected —
  no spurious retries, correct Polly telemetry in the logs.
- Wired in log4net as the sole logging sink (`ClearProviders()` +
  `AddLog4Net("log4net.config")`), console + daily-rolling file, with `Polly` and
  `System.Net.Http.HttpClient` categories tuned down to `WARN` to cut noise. The
  config file documents the existing PII policy in a comment rather than
  introducing new filtering — the code already only ever logs metadata. Verified
  both sinks live (console output and the actual `logs/syncagent.log` file
  contents).
- Migrated dispatch from the hand-rolled `SyncTaskDispatcher`/`ISyncTaskHandler`
  pair to the **Mediator** library (martinothamar) as originally specified: one
  `IRequest<IReadOnlyList<object>>` query record + one
  `IRequestHandler<TQuery,...>` per task type in Core, plus a
  `LoggingPipelineBehavior<TRequest,TResponse>` — a decorator wrapping every
  request with duration/record-count logging, so that concern lives in one place
  instead of inside each handler. `SyncTaskDispatcher` still keeps a small
  taskType-string → query-construction map, since Mediator dispatch is
  compile-time typed but the platform's `taskType` arrives as an untyped runtime
  string — documented as the one place a new task type costs more than "one line."
- Wrote the test suite: 6 dispatcher tests in `SyncAgent.Core.Tests` (built on a
  *real* Mediator pipeline via a throwaway `ServiceCollection`, not a hand-rolled
  fake `ISender` — exercises the same resolution path production uses), 3 worker
  loop-resilience tests in `SyncAgent.Worker.Tests` (platform-throws-forever
  doesn't crash the loop; 204 never posts; unknown task type still posts
  "failed"), and 8 tests in `SyncAgent.Infrastructure.Tests` — 4 contract tests
  comparing each record type's serialized JSON shape against
  `docs/sample-payloads/result-*.json` (all passed on the first run) and 4 SQL
  integration tests gated by `SYNCAGENT_TEST_DB_CONNECTION` via a custom
  `[SkippableFact]`, including a regression guard pinning the ProductInventory
  609-row count against the documented 1,069-row trap. All 17 pass; ran them both
  with and without the DB env var set to confirm the skip behavior is clean
  (reported as skipped, not failed) when no database is available.
- Hit and fixed a real gotcha while writing the dispatcher tests: `AddMediator()`
  is generated inside the Core assembly and auto-registers *every* real handler
  found there; resolving `ISender` eagerly builds Mediator's dispatch table, so
  even a test targeting one query needs all four real handlers' dependencies
  (`ISyncQueryRepository<TRecord>`) to be resolvable. Fixed by registering no-op
  fake repositories for all four record types in the test helper before
  overriding the handler under test.
- Ran one more full live end-to-end smoke test after all of the above landed
  (real simulator + real database + real Worker process) to confirm the whole
  stack works together, not just in isolation — confirmed via the log file that
  the pipeline behavior, log4net, and the resilience-wrapped HTTP client all fired
  correctly in the same request.
- Updated `docs/IMPLEMENTATION_PLAN.md` throughout to move every "planned" item to
  "implemented," documenting the Mediator testing gotcha and the final test
  breakdown.

**I did:** Asked for the robustness pass, and clicked the "Customers" button one
more time in the simulator's GUI to trigger the final live verification.

---

<!-- Add new entries above this line as work continues. -->

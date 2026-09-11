# Challenge Submission

## Candidate

- **Name:** Andrik
- **Date:** 2026-09-11

---

## How to Run

Prerequisites: .NET 10+ SDK (builds net48 via ReferenceAssemblies), .NET Framework 4.8 runtime,
AdventureWorks2025 on SQL Server/LocalDB, and the SyncPlatform app from `src/SyncPlatform`.

```bash
# build
cd candidate
dotnet build -c Release

# config: copy committed appsettings.json, fill real DB credentials into
# appsettings.Local.json (gitignored) — or set connection string via env override

# dev mode
SyncAgent\bin\Release\net48\SyncAgent.exe --console

# Windows Service (admin prompt)
sc create SyncAgent binPath= "C:\full\path\SyncAgent.exe" start= auto
sc failure SyncAgent reset= 86400 actions= restart/5000/restart/10000/restart/30000
```

Point the platform at `http://localhost:5100`; the API key is set in `appsettings.json`.

---

## Architecture Decisions

- **Client-side sync agent only** (per scope): a Windows Service (`ServiceBase`) polling loop.
- **Strategy pattern over switch.** One `ISyncTaskHandler` per task type
  (`GetCustomers`, `GetProducts`, `GetOrders`, `GetProductInventory`); the dispatcher
  resolves handlers by `taskType` at startup. A new task type = one new class + one DI line.
- **.NET Framework 4.8 target** to demonstrate working with older technology; SDK-style
  project compiled with the modern SDK, runtime ships with the 4.8 release framework.
- **`System.Data.SqlClient`** (not Microsoft.Data.SqlClient) — deliberate for the
  net48-era stack.
- **Parameterized SQL everywhere** (`@ModifiedSince`, `@MaxRows`), `TOP (@MaxRows)` row
  caps, 30 s command timeouts.
- **Fault isolation**: the whole poll iteration (get → dispatch → post) is try/caught;
  unknown task types and handler exceptions produce a `failed` result, never a crash.
- **204 = no work, not error**; the worker sleeps `PollIntervalMs` and re-polls.
- **Dedupe by `taskId`** (HashSet) — forward-looking idempotency; the current platform
  mints fresh ULIDs so redelivery is structurally impossible today.
- **Fail-fast config validation** before the service/loop starts.

---

## Security Measures

- No credentials committed: `appsettings.json` ships placeholders, real values go in
  `appsettings.Local.json` (gitignored) or environment overrides.
- API key sent via `X-Api-Key` header, configured in config (not hardcoded).
- SQL injection surface eliminated: every dynamic value is a `SqlParameter`; no string
  concatenation in queries.
- Query abuse prevention: `TOP (@MaxRows)` row cap + `CommandTimeout` on every command.
- 401 responses are logged and the loop continues (transient key rotation recovers
  without restart).
- Failed `POST /result` calls are logged for observability.

---

## Testing Strategy

- **Unit (18 tests, fast, green):**
  - Dispatcher: unknown type → `failed`; handler exception → `failed` (no rethrow);
    cancellation propagates; known type passes through.
  - Platform client (fake `HttpMessageHandler`): 200 parse / 204 → null / 401 throws;
    POST accepted true/false; camelCase payload assertions (both completed + failed).
  - Worker loop (fake client/dispatcher): duplicate taskId processed once; loop survives
    client failure and stops on cancel; cancel during backoff exits promptly.
  - Contract shape: result payloads compared structurally (types + nesting, incl.
    `orderDetails`) against the golden `docs/sample-payloads/result-*.json` examples.
- **Integration (real LocalDB, `[Category("Integration")]`, env `ADVENTUREWORKS_CONN`):**
  one test per handler — known value checks (customer 11000 → Jon Yang), nested order
  details grouping, `ModifiedSince` filter honored.
- **Manual E2E (executed)**: agent `--console` against the running SyncPlatform
  (localhost:5100) — all four task buttons produced a processed task:
  `accepted:true`, HTTP 200, no crash; `GetCustomers` 10000 rows, `GetProducts`
  504, `GetOrders` 10000 (nested order details), `GetProductInventory` 609,
  all filtered by the platform's `modifiedSince=2025-01-01Z`.
- **Hosting layer (Windows Service / SCM lifecycle)**: NOT yet smoke-tested.
  The ServiceBase code (OnStart fail-fast, EventLog writing, stop path) has
  never executed under `sc.exe`; the DB-connectivity-expected-to-fail-under-
  LocalSystem caveat from Known Limitations is part of that. Console mode is
  fully verified; service mode is implemented but unexercised.
- **With more time**: property-based payload tests, platform-side fault
  injection (flaky network), and the service smoke test (`sc create` → start →
  EventLog → stop → delete).

---

## Known Limitations

- LocalDB is per-user: a service running as LocalSystem cannot see `(localdb)\MSSQLLocalDB`.
  For service deployment point `ConnectionStrings:AdventureWorks` at a server instance or
  run the service as the LocalDB-owning user (dev only: use `--console`).
- Dedupe is forward-looking; if the platform starts redelivering tasks, the HashSet only
  covers in-process duplicates (restart clears it). A durable ack store would be the
  upgrade path.
- Integration tests need a local AdventureWorks2025 restore.
- Orders are capped `TOP (@MaxRows)` at header level, so a single task covers up to
  `MaxRows` orders (details for exactly those headers are included).

---

## AI Tools Used

- Used **opencode** (AI coding agent) to implement the solution end to end:
  scaffold, handlers, host, tests, and docs. The agent audited the repo first
  (`docs/api-contract.md`, `docs/sample-payloads/`, and the SyncPlatform source
  in `src/SyncPlatform`) to derive the contract, then built against it.
- Used **Claude** at the very start, before any code, to read the challenge
  repo (`CHALLENGE_DESCRIPTION.md`, `README.md`, `docs/api-contract.md`,
  `docs/sample-payloads/`, and the SyncPlatform source) and summarize the
  actual requirements — including behavior not spelled out in the top-level
  description, such as the `204 = no work` semantics, the nested
  `orderDetails` shape, and the API-key rejection behavior. Also used Claude
  with a live SQL connection to explore and verify the real AdventureWorks2025
  schema (tables, columns, foreign keys) before writing any query, instead of
  relying on memorized/assumed schema. That exploration was published as a
  browsable reference: https://claude.ai/code/artifact/f5a4d943-01e5-461c-a8ab-227fcbaab294
- Used **Claude** for plan audit (reviewed the implementation plan before
  coding, caught the phone-column bug and the N+1 orders pattern early) and for
  final implementation audit (independently built the solution, ran both test
  suites, and reviewed the submission for accuracy). The audit fixes applied
  from that pass: guaranteed order iteration in GetOrdersHandler and the
  corrections in this document.

---

## Time Spent

- Implementation + verification (commits): ~40 minutes (git timestamps
  2026-09-11 23:18–23:58). Heavy lifting done by the opencode agent; my role
  was reviewing the plan and accepting/rejecting each layer as it landed.
- Prior planning and research session (schema exploration, API contract,
  architecture discussion with Claude): ~1.5 h.
- Final audit pass (Claude review + fixes): ~30 min.
- Total: ~2.5 h. Deliberately AI-assisted — the challenge asks for it, and the
  git history is public evidence of the actual span.

---

## Feedback

- The challenge is well-scoped and the `docs/` folder is excellent. The `204 = no work`
  nuance is easy to get wrong without the PoC doc — glad it was explicit. One suggestion:
  a short note in README pointing out that `candidate/` is the expected work directory
  (it was, but only via the challenge description).
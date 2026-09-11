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
- **Manual E2E**: agent `--console` against running SyncPlatform, all four task types,
  verify `accepted:true` + `recordCount > 0`.
- **With more time**: property-based payload tests, platform-side fault injection
  (flaky network), and service-install integration script.

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

- Used **opencode** (AI coding agent, model-based) to implement the solution end to
  end: scaffold, handlers, host, tests, and documentation. The agent also audited the
  repo (schema, SyncPlatform task queue, payload samples) and reviewed its own diffs
  against the hidden requirements in `docs/SyncAgentPoC.md`.

---

## Time Spent

- ~1.5 h planning + research (schema, API contract, platform behavior)
- ~3 h implementation (scaffold → handlers → host → tests)
- ~0.5 h E2E verification and docs

---

## Feedback

- The challenge is well-scoped and the `docs/` folder is excellent. The `204 = no work`
  nuance is easy to get wrong without the PoC doc — glad it was explicit. One suggestion:
  a short note in README pointing out that `candidate/` is the expected work directory
  (it was, but only via the challenge description).
# Challenge Submission

## Candidate

- **Name:** Pablo García Fernández 
- **Date:** 2026-09-30

---

## How to Run

<!-- Provide step-by-step instructions to build and run your solution -->

### Prerequisites

- .NET SDK 9 or later (the solution targets `net9.0`)
- AdventureWorks OLTP restored as `AdventureWorks2025` (see the root `README.md`).
  I developed against **SQL Server 2025 LocalDB** (`(localdb)\AW2025`), but any instance works — only the connection string changes.

### 1. Configure secrets

The API key and the connection string are **not** stored in `appsettings.json` (see *Security Measures*).
Provide them with .NET User Secrets (run from the `candidate/` folder):

```powershell
dotnet user-secrets set "Platform:ApiKey" "candidate-test-key-2026" --project src\SyncAgent.Worker

dotnet user-secrets set "ConnectionStrings:AdventureWorks" 'Server=(localdb)\AW2025;Database=AdventureWorks2025;Integrated Security=True;TrustServerCertificate=True;Application Name=SyncAgent' --project src\SyncAgent.Worker

dotnet user-secrets list --project src\SyncAgent.Worker
```

Alternatively, use environment variables (standard .NET configuration, `__` = section separator):

```powershell
$env:Platform__ApiKey = "candidate-test-key-2026"
$env:ConnectionStrings__AdventureWorks = 'Server=(localdb)\AW2025;Database=AdventureWorks2025;Integrated Security=True;TrustServerCertificate=True'
```

> Adjust `Server=` to your instance, e.g. `Server=localhost` for a default SQL Server instance.
> If either value is missing, the agent fails at startup with a message saying what is missing.

### 2. Start the test platform

```powershell
cd src\SyncPlatform
dotnet run --project SyncPlatform
```

### 3. Run the sync agent

```powershell
cd candidate
dotnet run --project src\SyncAgent.Worker
```

Click the task buttons in SyncPlatform; the agent picks up each task, queries the database and posts the result (visible in the platform's log viewer).

### 4. Run the tests

```powershell
cd candidate
dotnet test
```

---

## Architecture Decisions

<!-- Explain your project structure and key design decisions. Why did you choose this approach? -->

### Project structure

```
candidate/
  src/
    SyncAgent.Core/            Contracts, models, validation, dispatcher. No infrastructure dependencies.
    SyncAgent.Infrastructure/  SQL (Dapper) handlers, platform HTTP client, options, DI registration.
    SyncAgent.Worker/          Host: Program.cs, PollingWorker, appsettings.
  tests/
    SyncAgent.Tests/           xUnit unit tests.
```

Dependencies point inwards: **Worker → Infrastructure → Core**. Core knows nothing about HTTP, SQL or configuration, so the business rules (what a valid task is, how a task becomes a result) can be tested in isolation, and infrastructure can change without touching them.

### Application type: .NET Worker Service

The agent must be *always-on*, so I used the Generic Host (`dotnet new worker`): it gives dependency injection, configuration, logging and graceful shutdown out of the box, and the same project can be installed as a Windows Service without code changes to the core logic.

### Main flow

```
PollingWorker ──GET next-task──> IPlatformClient
      │
      └─> ISyncTaskDispatcher ──validate──> ISyncTaskValidator
                    │
                    └─> ISyncTaskHandler (selected by TaskType) ──> SQL Server
      │
      └──POST result──> IPlatformClient
```

- **`PollingWorker`** only orchestrates: poll, dispatch, post. It polls again immediately while tasks are queued, waits `PollingIntervalSeconds` when the queue is empty, and applies **exponential backoff** (capped at `MaxBackoffSeconds`) on any error, so it never crashes.
- **`SyncTaskDispatcher`** validates the task, picks the handler and always returns a `SyncResult` (`completed` or `failed`). Task errors never escape as exceptions; only shutdown cancellation does.
- **`PlatformClient`** is a typed `HttpClient`; authentication is added by a separate `ApiKeyHandler` (`DelegatingHandler`), so the client has a single responsibility.

### Adding a new task type (extensibility)

Each task type is one class implementing `ISyncTaskHandler`. The dispatcher receives all registered handlers from DI and indexes them by `TaskType`, so there is no `switch` to update (Open/Closed principle). For single-query tasks, the base class `SqlQueryHandler<TRecord>` does all the plumbing (connection, parameters, timeout, limit), so a new task is:

1. A record class with the output fields.
2. A handler class defining `TaskType` and the SQL (`@ModifiedSince`, `TOP (@MaxRecords)`).
3. One line in `DependencyInjection.cs`: `services.AddSingleton<ISyncTaskHandler, NewHandler>();`

`GetOrders` implements `ISyncTaskHandler` directly because it needs two result sets and in-memory nesting.

### Data access: Dapper + explicit SQL

I chose Dapper over EF Core because the job is read-only extraction from databases I don't own: explicit SQL is easy to review, tune and explain to a DBA, and there is no model or migrations to maintain. Each query lives next to its handler as a raw string literal, with column aliases matching the payload field names.

Query decisions worth noting:

- **Customers:** only individual customers (with a `Person`). A person can have several emails, phones and addresses, so `OUTER APPLY (SELECT TOP 1 …)` returns exactly one row per customer.
- **Products:** `LEFT JOIN` to subcategory/category so products without a category are not dropped.
- **Orders:** the IDs of the matching orders are stored once in a table variable (with the `TOP` limit applied), and both headers and details are read from it with `QueryMultiple`. This keeps headers and lines consistent, needs one round trip, and avoids the 2,100-parameter limit of `IN (@ids)`. `customerName` falls back to the store name when the customer has no person. `recordCount` counts orders, not lines.
- **Dates:** SQL `datetime` has no time zone, so Dapper returns `DateTimeKind.Unspecified`. A `UtcDateTimeConverter` writes every date as ISO 8601 UTC with `Z`, matching the sample payloads (assumes the database stores UTC).

### Other decisions

- **Validated input object:** handlers receive a `SyncQuery` built by the validator, never the raw task, so they can trust their input.
- **Options pattern with fail-fast validation** (`ValidateOnStart`) for platform, sync and database settings.
- **`TimeProvider` injection** for deterministic tests of date rules and timestamps.
- **Singleton handlers:** they are stateless and open a connection per execution (pooled by SqlClient).
- **HttpClient lifetime:** the typed client lives inside a singleton hosted service, so connection recycling is handled by `SocketsHttpHandler.PooledConnectionLifetime` instead of relying on `IHttpClientFactory` handler rotation.

---

## Security Measures

<!-- What security measures did you implement and why? -->

### Secrets kept out of source control

- The platform API key and the database connection string are credentials, so they are never committed.
  In development they come from **.NET User Secrets** (stored in the user profile, outside the repo);
  in production they would come from **environment variables** or a secret store (e.g. Azure Key Vault) — no code change needed, since .NET configuration reads all of these sources.
- Even though the API key here is a public test key, the agent is meant to run on client machines, so treating it as a real secret is the right default.

### Fail-fast configuration validation

- `PlatformOptions`, `SyncOptions` and `DatabaseOptions` are validated with Data Annotations + `IValidatableObject` + `ValidateOnStart()`, so a misconfigured agent refuses to start instead of failing on the first request.
- `BaseUrl` must use **HTTPS** unless it points to localhost, because the API key travels in a request header.
- The connection string must be well-formed and name both a server and a database (otherwise queries would silently run against the login's default database). Validation messages never include the connection string itself, since it may contain credentials.

### SQL injection

- Every query is **parameterized** (`@ModifiedSince`, `@MaxRecords`) through Dapper; no input value is ever concatenated into SQL text.
- `taskType` is never used in SQL: it only selects a handler from a fixed set registered in DI (allow-list).

### Untrusted task input

Tasks coming from the platform are treated as untrusted and validated in `SyncTaskValidator` **before** reaching any handler:

- `taskId` must be a valid ULID (26 chars, Crockford base32).
- `taskType` must match a registered handler (exact, case-sensitive).
- `modifiedSince` is required and must be within a sane range (not before 1900, not in the future beyond a small clock skew).
- Handlers only receive a validated, normalized `SyncQuery` — never the raw payload.
- Rejection messages sent back to the platform are fixed strings and never echo the input.

### Abuse and resource limits

- **`MaxRecords`** (`TOP (@MaxRecords)`) caps how much data a single task can extract; a warning is logged when results are truncated.
- **`CommandTimeout`** bounds how long a query can run against the client's database.
- Tasks are processed **sequentially**, so a flood of queued tasks cannot trigger parallel heavy queries.
- **De-duplication** of recently processed `taskId`s (bounded in-memory set) prevents executing a re-delivered task twice.
- HTTP **request timeout** and a **1 MB response size cap** protect the agent against slow or oversized responses.
- **Exponential backoff** on errors avoids hammering the platform when it is down or rejecting requests.

### No information leakage

- When a task fails, the platform only receives a **generic error message**; exception details (SQL errors, stack traces, server names) stay in the local logs.
- The `X-Api-Key` header is **redacted** from HttpClient logs, and HttpClient logging is lowered to `Warning` to avoid noise from polling.
- Untrusted values (`taskId`, `taskType`) are **sanitized** before logging (control characters removed, length capped) to prevent log forging.

### Least-privilege database access (not implemented — recommended for production)

For this challenge the agent connects with Windows authentication, which on LocalDB means a `sysadmin` login.
In production the agent should use a **dedicated read-only principal** (SQL login or a Windows service account) that only has `SELECT` on the specific tables it queries (`Sales.Customer`, `Sales.Store`, `Sales.SalesOrderHeader`, `Sales.SalesOrderDetail`, `Person.Person`, `Person.EmailAddress`, `Person.PersonPhone`, `Person.BusinessEntityAddress`, `Person.Address`, `Person.StateProvince`, `Person.CountryRegion`, `Production.Product`, `Production.ProductSubcategory`, `Production.ProductCategory`, `Production.ProductInventory`, `Production.Location`).
Permissions should be granted **per table rather than per schema**, since schema-level `SELECT` on `Person` or `Sales` would also expose sensitive tables such as `Person.Password` or `Sales.CreditCard`.
This way, even if the agent were compromised, it could not modify data or read anything beyond what the platform is meant to receive.

### Other production considerations

- `TrustServerCertificate=True` is only acceptable for local development; in production the connection should use `Encrypt=True` with a trusted certificate.
- If installed as a Windows Service, the agent should run under a low-privilege service account, not `LocalSystem`.

---

## Testing Strategy

<!-- What did you test and why? What would you test with more time? -->

Tests live in `candidate/tests/SyncAgent.Tests` (xUnit + Shouldly). I focused on the parts where a bug would either break the contract with the platform or weaken security.

### Unit tests

All tests run without a database or network, so `dotnet test` works on any machine.

| Area | What is covered | Why |
|---|---|---|
| `SyncTaskValidator` | Valid task; invalid/malformed ULIDs; unknown or wrong-case `taskType`; missing parameters; `modifiedSince` out of range, within clock skew, and `Unspecified` kind normalized to UTC | It is the security boundary for untrusted input |
| `SyncTaskDispatcher` | Routing to the right handler; completed result shape (`recordCount`, UTC `executedAt`); invalid tasks never reach a handler; handler exceptions produce a **generic** error (no leaked details); shutdown cancellation propagates, other cancellations are reported as failures; duplicate handlers fail fast | Core orchestration and the "never leak internals" rule |
| `PlatformClient` | 204 → no task; 200 → camelCase deserialization; 401/5xx/malformed JSON throw (so the worker backs off); correct endpoints; result body shape matches the API contract, including dates written as UTC with `Z`; 400 on result throws | Contract with the platform |
| `ApiKeyHandler` | Header added to every request and replaced (not duplicated) if already present | Authentication |
| `UtcDateTimeConverter` | `Unspecified`/`Local`/`Utc` values written with `Z`; offsets normalized to UTC when reading | SQL `datetime` has no time zone; payload dates must match the samples |
| `PlatformOptions` / `DatabaseOptions` | HTTPS required for non-local hosts, backoff vs interval, API key and URL rules; connection string format, server and database required, credentials never echoed in messages | Fail-fast configuration and secret hygiene |
| `LogSanitizer` | Control characters removed, long values truncated | Log forging prevention |

Test doubles are hand-written (`FakeSyncTaskHandler`, `StubHttpMessageHandler`, `FixedTimeProvider`) so the tests stay readable and don't depend on a mocking library. `TimeProvider` is injected so date rules are deterministic.

### SQL queries

The handler queries were verified manually against AdventureWorks (SSMS and end-to-end runs with SyncPlatform), comparing field names, counts and nested order details with `docs/sample-payloads/`.

### With more time

- Integration tests running each handler against a real AdventureWorks database (e.g. LocalDB or a Testcontainers SQL Server) to verify mapping, the `modifiedSince` filter, `MaxRecords`, and that order details are nested with the limit applied to orders, not lines.
- `PollingWorker` tests (backoff sequence, immediate re-poll while tasks are queued, de-duplication) using a fake `TimeProvider`-driven delay.
- Contract tests that compare each handler's output field-by-field with `docs/sample-payloads/`.
- An end-to-end test hosting a fake platform with `WebApplicationFactory`/Kestrel.

---

## Known Limitations

<!-- Be honest about trade-offs you made due to time constraints. What would you improve? -->

- **No pagination.** Results are capped by `MaxRecords` (default 5,000) and a warning is logged when the cap is hit, but the platform has no way to request the remaining records. With a contract change I would add keyset pagination (e.g. `afterId` + `pageSize`) or send results in chunks.
- **Change detection is per main table.** Customers are filtered by `Sales.Customer.ModifiedDate` only, so a change to just an email, phone or address is not detected; the same applies to order lines vs. `SalesOrderHeader`. Using the latest `ModifiedDate` across the joined tables would fix it at the cost of a more expensive query.
- **Customers only include individuals.** Store customers without a `Person` are excluded, following the sample payload fields (`firstName`, `lastName`, …).
- **One email/phone/address per customer.** When there are several, one is picked deterministically (`TOP 1 … ORDER BY`), but a business rule (e.g. prefer "Home" address) should confirm which.
- **Dates are assumed to be UTC** in the database; there is no per-client time-zone configuration.
- **Result posting is not retried.** If `POST /result` fails, the error is logged and the task is not re-executed (it is already in the de-duplication set), so that result is lost. A retry policy (e.g. `Microsoft.Extensions.Http.Resilience`) or a local outbox would fix it.
- **De-duplication is in memory** and bounded to the last 1,000 task IDs, so it is lost on restart.
- **Sequential processing.** One task at a time is safer for the client database but limits throughput; a bounded concurrency setting could be added if needed.
- **Large payloads are held in memory** before serializing; streaming the response body would reduce memory use for big result sets.
- **Least-privilege SQL principal not implemented** (see *Security Measures*); the agent uses Windows authentication.
- **Not yet packaged as a Windows Service.** The Worker Service template supports it (`AddWindowsService()` + `sc create`), but I did not add or test it.
- **No integration or `PollingWorker` tests** (see *Testing Strategy*).

---

## AI Tools Used

<!-- Which AI tools did you use? How did you use them? Be specific.
     Examples:
     - "Used GitHub Copilot for autocomplete while writing query classes"
     - "Used ChatGPT to research AdventureWorks schema relationships"
     - "Used Claude to generate unit test boilerplate" -->

I used **Claude** (Anthropic) throughout the challenge as a pair-programming assistant. I drove the process step by step, typed or pasted the code myself, built and ran it locally, and reviewed each piece before committing.

- **Environment setup:** diagnosing why the `AdventureWorks2025.bak` restore failed (backup made on SQL Server 2025 / v17, my LocalDB was 2019 / v15) and setting up a SQL Server 2025 LocalDB instance.
- **Planning:** breaking the challenge into steps, and discussing the architecture (Worker Service, Core/Infrastructure/Worker split, handler-per-task design).
- **SQL:** drafting the four queries from the sample payloads and the AdventureWorks schema; I validated them in SSMS against the real data.
- **Code:** generating the initial versions of the options classes, contracts, validator, dispatcher, platform client, polling worker and handlers, which I then adapted to my project structure. It also reviewed my repository at several points and caught issues (e.g. an inverted project reference between Core and Infrastructure, logs that still used unsanitized values).
- **Security:** brainstorming abuse scenarios and mitigations (input validation, limits, log redaction and sanitization, least privilege).
- **Tests:** the unit test suite was written by Claude against my code; I ran it and fixed what was needed.
- **Documentation:** drafting most of this `CHALLENGE_SUBMISSION.md`, which I reviewed and edited.

---

## Time Spent

<!-- Approximate breakdown of how you spent your time -->

| Activity | Time |
|---|---|
| Environment setup (SQL Server / LocalDB 2025, AdventureWorks restore, SSMS) | 2 h |
| Exploring the database and writing the SQL queries | 1 h |
| Solution structure, configuration and secrets | 1 h |
| Polling loop, platform client and dispatcher | 2 h |
| Task handlers | 1 h |
| Security hardening | 1 h |
| Tests | 0.5 h |
| Documentation | 0.5 h |
| **Total** | **9 h** |

This is above the suggested 2 hours. A significant part went into environment setup, and I chose to go beyond a minimal solution in validation, security and tests, and to understand each piece rather than paste a generated solution.

---

## Feedback

<!-- Any feedback on the challenge itself? Was anything unclear? What would you change? -->

- **SQL Server version requirement.** The README lists "LocalDB, Express, or Developer Edition" but `AdventureWorks2025.bak` can only be restored on **SQL Server 2025**. On older versions (e.g. LocalDB 2019, which ships with Visual Studio) the restore fails with a version error. Stating the required version, or offering the older `.bak` files as an alternative, would save candidates setup time.
- **Restore instructions for LocalDB.** LocalDB needs `WITH MOVE` to place the data files; a short note with `RESTORE FILELISTONLY` would help.
- **Contract details that could be explicit:** whether `modifiedSince` is always present, the expected time zone of dates, whether a result can be posted more than once, and the expected behaviour for large result sets (paging).
- Overall, the challenge is realistic and well scoped: exploring an unfamiliar database and building a small, secure agent around a clear API contract is a good reflection of the real job.

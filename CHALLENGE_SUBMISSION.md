# Challenge Submission

## Candidate

- **Name:** JCique_Europea
- **Date:** 2026-09-09

---

## How to Run

```bash
# 1. Build and test
dotnet build candidate/SyncAgent.sln -c Release
dotnet test  candidate/SyncAgent.sln -c Release

# 2. Point the agent at your database
#    Edit candidate/src/SyncAgent.Host/App.config, or set SYNCAGENT_CONNECTIONSTRING

# 3. Start the provided platform
dotnet run --project src/SyncPlatform/SyncPlatform/SyncPlatform.csproj

# 4. Start the agent
candidate\src\SyncAgent.Host\bin\Release\net48\SyncAgent.Host.exe --console
```

Click the four sync buttons in the platform window. Each produces
`RESPONSE 200 OK — Result accepted` in its log pane, and a matching line in the agent's
console and in `logs/sync-agent-<date>.log`.

`dotnet build` compiles `net48` without Visual Studio: the solution references
`Microsoft.NETFramework.ReferenceAssemblies`, so the .NET SDK alone is enough.

Full detail — configuration reference, how to add a task type, Windows Service install — is in
[`candidate/README.md`](candidate/README.md).

---

## Architecture Decisions

**.NET Framework 4.8, not .NET 8.** The role this is for builds .NET Framework 4.8 desktop
software, and the challenge leaves the application type open. Targeting the runtime the work
actually happens on seemed more useful than targeting the newest one. Projects are SDK-style
and reference `Microsoft.NETFramework.ReferenceAssemblies`, so a reviewer with only the .NET
SDK can still build and test everything. C# 9 is enabled via an `IsExternalInit` shim linked
from `Directory.Build.props`.

**Three production assemblies, with the dependency direction enforced by project references.**
`Core` holds the domain, the dispatcher, validation, retry and the polling loop, and
references no I/O library at all — only logging abstractions. `Infrastructure` owns every
piece of I/O. `Host` is the only place that knows a concrete type. This is what lets the
entire behaviour of the agent be tested in under a second with no database and no sockets.

**One handler per task type, behind `ISyncTaskHandler`.** The dispatcher builds its routing
table from whatever handlers the container gives it, so adding a task type is a new class plus
one registration line — the dispatcher, the worker and the platform client are closed for
modification. All four current types differ only in which query they run, so the shared work
lives in a `ModifiedSinceSyncTaskHandler<TRecord>` base and a concrete handler is about twenty
lines. A duplicate task-type registration throws at startup rather than letting container
ordering decide which handler wins.

**Raw ADO.NET rather than Dapper or EF.** The challenge is fundamentally about reading someone
else's database correctly, so the SQL is worth being explicit about: every statement is a
compile-time constant, every value is a typed `SqlParameter`, and there is exactly one place
in the codebase where a parameter is bound to a command. That also makes the injection story
trivially auditable — no query concatenates SQL and none of them can.

**A hand-written retry policy instead of Polly.** Polly is what I would use in production. Here
the retry semantics are part of what is being assessed, and sixty lines that can be asserted
directly seemed worth more than a dependency whose behaviour has to be taken on trust.
Exponential backoff with full jitter; what counts as transient is a separate abstraction, so
SQL Server and HTTP can disagree about it without either answer being hard-coded into the
backoff loop.

**Newtonsoft.Json instead of System.Text.Json.** On `net48`, STJ pulls in `System.Memory`,
`System.Runtime.CompilerServices.Unsafe` and `System.Text.Encodings.Web`, each wanting a
binding redirect. Newtonsoft has no dependencies on this TFM.

**Console and Windows Service in one executable.** `Environment.UserInteractive` picks the
mode, so a reviewer presses F5 and a client machine gets the same binary under the SCM, with
no separate console build to drift out of sync.

**Three things about the platform shaped the design, none of which are in `api-contract.md`:**

1. *The result body must be camelCase.* The platform deserialises with
   `JsonSerializer.Deserialize<SyncResult>(body)` and no options, so
   `PropertyNameCaseInsensitive` is false while its model carries lowercase
   `[JsonPropertyName]` attributes. A body with default .NET naming binds nothing at all:
   deserialisation succeeds, every field stays empty, and you get
   `400 Missing required field: taskId` for a body that visibly contains one. The failure
   points at the wrong side of the wire, so there is a dedicated set of contract tests to keep
   it from regressing.
2. *The dequeue is destructive.* `GET /api/sync/next-task` removes the task with no ack, no
   lease and no redelivery, so from the moment it returns the agent holds the only copy.
   Everything else follows: every path through the worker ends in a submitted result, shutdown
   included; a failed submission logs everything needed to requeue by hand; and a machine-wide
   mutex stops two agents silently splitting the queue.
3. *The listener binds `localhost`, not `+`.* `http://127.0.0.1:5100` is rejected by http.sys
   before reaching the handler.

**Query decisions the sample payloads do not make for you** are documented at each query, with
the reasoning and the row counts behind them: excluding the 701 person-less store accounts
from `GetCustomers`, `LEFT JOIN`ing product categories so the 209 products without a
subcategory are not dropped, deterministic tie-breaks on every `TOP 1` sub-select, and taking
`accountNumber` on orders from `Sales.Customer` rather than the differently-shaped column of
the same name on `SalesOrderHeader`.

---

## Security Measures

**SQL injection.** Every statement is a `const string`; every value arrives as a typed
`SqlParameter` through a single factory. There is no dynamic SQL anywhere, and `taskType`
never reaches a query — it selects a handler from a closed registry.

**The platform is treated as an untrusted peer.** It speaks plain HTTP over loopback, and the
agent has no way to authenticate it — only the reverse. So `taskId` is checked against the
ULID alphabet, `taskType` is length-capped, parameter count and value lengths are capped, and
response bodies are read through a bounded reader that checks the declared `Content-Length`
first and then caps the read itself, because `Content-Length` is a claim by the sender.

**Denial of service, in both directions.** `modifiedSince` is rejected below a configured
minimum, because a cutoff of `0001-01-01` turns an incremental sync into a full scan of the
client's production database. Rows are capped inside the SQL, not trimmed afterwards. Result
bodies are size-checked before sending, since the platform enforces no limit of its own.
Command and HTTP timeouts are explicit, retries are bounded with jittered backoff, and a
failing poll cycle backs off instead of spinning.

**Least privilege at the database.** [`candidate/sql/01-create-sync-agent-login.sql`](candidate/sql/01-create-sync-agent-login.sql)
provisions a login with `SELECT` on exactly the seventeen tables the queries name — and
`DENY SELECT` on the `HumanResources` and `Purchasing` schemas, so someone later adding the
account to `db_datareader` to "fix a permissions error" cannot widen its reach back to
`Person.Password` and `Sales.CreditCard`.

**Secrets.** The API key and connection string are never in code. They come from `App.config`
with environment-variable override, and both sections can be DPAPI-encrypted at rest with
`aspnet_regiis -pe` and no code change. Startup logs the effective configuration with the key
reduced to a prefix and a length, and the connection string rebuilt from an allow-list of
keywords rather than regex-stripped — a regex over `Password=` misses the `Pwd` alias and the
escaping rules for a password containing a semicolon, and it fails open.

**Error hygiene.** What travels back to the platform is the exception type name and the task
id, never the message. A `SqlException` message can name the server, the database and part of
the statement; an `IOException` can name a local path. The full detail stays in the local log,
and a test asserts that a message like `Login failed for user 'sa' on server PROD-SQL-01` does
not leave the process.

**Transport.** `Encrypt=True` on the connection string; `ServicePointManager.SecurityProtocol`
pinned to TLS 1.2+, which costs nothing against a loopback test server and matters the moment
the agent points at a real HTTPS endpoint.

**Single instance.** A `Global\` mutex, because the destructive unsynchronised dequeue means a
second agent would not duplicate work — it would silently split the queue across two processes
and two log files.

---

## Testing Strategy

**85 unit tests**, no database and no sockets, under a second. **11 integration tests** against
a real AdventureWorks, opted into with `SYNCAGENT_TEST_CONNECTIONSTRING` and skipped otherwise,
so `dotnet test` stays green for a reviewer who has only the SDK.

The two areas I put the most effort into:

*The JSON contract.* These serialise the real DTO — reached through `InternalsVisibleTo` rather
than a hand-written mirror that could drift and keep passing — and compare its key set against
`docs/sample-payloads` for all four record shapes plus the nested order lines. They also pin
the two things nothing else would catch: `status` as the exact lowercase literals the platform
compares with ordinal equality, and timestamps ending in `Z` rather than `+00:00`. This is the
failure mode that costs an afternoon, so it gets tests that fail loudly.

*The destructive dequeue.* Worker tests cover every way a task can go wrong — success,
unsupported type, validation failure, unexpected exception, interruption by shutdown — and
assert that each one still produces a submitted result, and that a failure to submit does not
kill the loop.

Time-dependent behaviour goes through an `IDelayProvider` and an `ISystemClock`, so backoff
growth is asserted rather than hoped for and the suite never sleeps. Retry tests pin the
jitter draw at its maximum so the ceiling is observable; with a live draw the delays are
uniform over `[0, ceiling]` and only assertable statistically.

Integration tests assert against the fixed identifiers from the sample payloads, and four of
them pin decisions rather than mechanics: a future cutoff returns nothing from all four queries
(without which a query ignoring `modifiedSince` would pass everything else), every customer has
a name, the product count is 504 with nulls allowed for category, and two consecutive runs are
equivalent with strict ordering.

**With more time:** a test double for the platform's HTTP server so the whole loop can be
exercised over a real socket; a load test with a synthetic table larger than
`MaxRecordsPerTask`; and a test that the DPAPI-encrypted configuration path actually round
trips, which I verified by hand but did not automate.

---

## Known Limitations

- **Truncation is invisible to the platform.** The contract has no field for it, so a sync
  clipped by `MaxRecordsPerTask` is reported as a normal completion. The agent logs a warning
  locally, but a real deployment needs a `hasMore` flag and a continuation token — otherwise a
  partial extract is indistinguishable from a small one.
- **The orders query's two statements are not in one transaction.** They run in one batch,
  which keeps the window small, and a line inserted between them arrives on the next sync.
  Snapshot isolation would close it on a database with `READ_COMMITTED_SNAPSHOT` enabled.
- **Customers with no associated person are not synced** — 701 of 19,820. The contract's record
  shape requires a first and last name, and emitting nulls would push unusable records into the
  platform. This is a question for whoever owns the contract rather than something to decide
  silently, and it is the first thing I would raise.
- **The Windows Service lifecycle was not exercised end to end.** The code is conventional
  `ServiceBase` and `ManagedInstallerClass` usage and the build is verified, but I did not run
  `install` / `net start` / `net stop` on the development machine.
- **No circuit breaker.** The idle backoff caps polling at once a minute against a dead
  platform, which is enough here, but a fleet of agents would want to stop hammering a platform
  that is failing rather than merely slow.
- **`GetCustomers` filters on `Customer.ModifiedDate OR Person.ModifiedDate` only.** A changed
  address or e-mail does not currently mark the customer as modified. Adding four more OR'd
  date columns would cost the index strategy for a case the contract does not describe, so I
  left it and wrote it down.

---

## AI Tools Used

I used **Claude Code (Opus)** throughout, as the challenge encourages. Concretely:

- **Schema exploration.** Rather than guessing at AdventureWorks, I had it run `sqlcmd`
  queries to map each sample payload to real tables and to check the assumptions — that 701
  customers have a null `PersonID`, that 209 products have no subcategory, that a person can
  hold two addresses, that `accountNumber` in the orders sample comes from `Sales.Customer` and
  not `SalesOrderHeader`. Every row count quoted in the code comments came from an actual query
  against the restored database, not from memory.
- **Reading the platform's source instead of only its documentation.** That is where the
  camelCase binding behaviour, the destructive dequeue and the `localhost`-only listener prefix
  came from. None of the three are in `api-contract.md`, and all three shaped the design.
- **Writing the code and tests**, with me directing the architecture: the `net48` target, the
  three-assembly split, ADO.NET over an ORM, the hand-written retry, the console/service host.
  I reviewed every file; a few things came back wrong and were changed — an over-clever
  exception shim in the transient detector, and a contract test that was asserting the test
  host's locale because `JObject.Parse` converts ISO strings to `DateTime`.
- **End-to-end verification.** It drove the platform's WPF buttons through UI Automation and
  read back its log pane, which is how the four record counts in this document were confirmed
  rather than assumed.

**Environment note that cost real time:** the `AdventureWorks2025.bak` in the README is a
SQL Server 2025 backup (internal version 998) and will not restore on SQL Server 2022 (957).
I installed SQL Server 2025 Express alongside. Worth knowing, because the AdventureWorks 2022
backup has its dates eleven years earlier, so the hardcoded `modifiedSince` of
`2025-01-01T00:00:00Z` would return zero rows for every task and look like a bug in the agent.

---

## Time Spent

Roughly four hours, against a two-hour estimate. The overrun was mostly environment — the
SQL Server 2025 install above — and the depth of the code comments and commit messages, which
I extended past what I would write day-to-day because the brief asks for the reasoning to be
visible.

| | |
|---|---|
| Environment: SQL Server 2025, restoring AdventureWorks | ~45 min |
| Reading the contract, the platform source and the schema | ~40 min |
| Core, handlers, queries | ~70 min |
| Platform client, retry, worker, host | ~50 min |
| Tests | ~45 min |
| Documentation and commit messages | ~30 min |

---

## Feedback

The challenge is well put together, and the deliberate omission — telling you to explore
AdventureWorks yourself — is the right call. Working out that a customer can have two
addresses and that the payload has room for one is exactly the kind of decision the job
involves.

Three things that cost time without teaching anything:

1. **The `.bak` link only works on SQL Server 2025.** `AdventureWorks2025.bak` is internal
   version 998 and fails on 2022 with a message about supported versions. The README's example
   path (`MSSQL17.MSSQLSERVER`) implies 2025, but the prerequisites just say "SQL Server". One
   line saying "requires SQL Server 2025 or later" would save every candidate on an older
   instance the same detour — and the failure mode if they use the 2022 backup instead is
   worse than an error, because the dates are eleven years earlier and every task silently
   returns zero rows.

2. **The camelCase requirement is invisible until it bites.** The platform deserialises with
   case sensitivity on, so a result body with default .NET property naming binds nothing and
   comes back as `400 Missing required field: taskId` — an error that describes the body as
   missing a field it plainly contains. Since `api-contract.md` documents the field names but
   not the binding behaviour, a candidate can lose an hour debugging their own correct code. I
   would either say so in the contract or set `PropertyNameCaseInsensitive = true`.

3. **A queue that only fills from a button click** makes it awkward to test anything about
   sustained polling, concurrency or backoff. A checkbox to enqueue on a timer, or a small
   `POST /api/sync/enqueue`, would make the agent's steady-state behaviour observable.

One genuine ambiguity worth resolving in the contract itself: what should happen to the 701
customers in `Sales.Customer` with no associated person? Their `firstName` and `lastName`
cannot be populated, and every candidate will silently pick a different answer.

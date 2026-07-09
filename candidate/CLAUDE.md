# SyncAgent — Engineering Standards

Binding rules for all work in `candidate/`. These override default behaviour.

## The commit rule (non-negotiable)

**Every commit ships the production code *and* the tests for that code, together.**

A commit that adds `TaskValidator.cs` without `TaskValidatorTests.cs` is not a
valid commit. There is no "tests come later" phase. If a commit's code has no
observable behaviour to test, say so explicitly in the commit message rather
than silently omitting tests.

Before **each** commit, all three of these must hold. Run them; do not assume.

```bash
cd candidate
dotnet build SyncAgent.sln --nologo                 # 0 warnings, 0 errors
dotnet test  SyncAgent.sln --nologo                 # all green, and a non-trivial test count
dotnet test  SyncAgent.sln --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

Never report "tests pass" when the suite is empty or only contains scaffolding
from `dotnet new`. A green run over zero real tests is a false signal — state
the actual number of tests that ran.

## Coverage

- Target **≥ 90% line coverage** on `Sync/`, `Validation/`, `Platform/`.
- Excluded from the target, by design: `Program.cs` (DI wiring, exercised by
  running the app), `Data/Queries/SyncQueries.cs` (const SQL strings), and DTOs.
- Coverage is a floor, not a goal. A test that raises coverage without being
  able to fail is worse than no test. Assert on behaviour, not on "it ran".

## Verification beyond unit tests

`dotnet test` proves the units work. Before the final commit, prove the *agent*
works: launch `src/SyncPlatform`, run the agent, click all four task buttons,
and confirm `accepted: true` with a non-zero `recordCount` for each of
`GetCustomers`, `GetProducts`, `GetOrders`, `GetProductInventory`.

Integration tests that touch the real `AdventureWorks2025` database are tagged
`[Trait("Category", "Integration")]` and excluded from the default run.

## Code quality

- **SOLID.** Especially: a new sync task type must be one new class implementing
  `ISyncTaskHandler` plus one DI registration. Nothing else may change.
- `TreatWarningsAsErrors` stays on. Do not suppress a warning to make it pass.
- Nullable reference types enabled and honoured; no `!` without a comment
  explaining why the invariant holds.
- No `DateTime.UtcNow` / `DateTime.Now` in production code — inject
  `TimeProvider` so behaviour is testable without sleeping.
- Every externally-supplied value is validated at a single trust boundary before
  it reaches SQL. Parameters bind as `DbParameter`; never string-concatenate SQL.
- Secrets (the API key) come from configuration, never from source, and are
  never written to a log — log the header *name*, never its value.
- Comments explain *why*, never *what*. Do not write comments addressed to a
  code reviewer ("this fixes...", "as requested").

## Commit messages

The grader reads the commits. Each message explains the reasoning, not the
diff: what decision was made, what alternative was rejected, and why. Subject
line in the imperative, `type(scope): summary`.

End every commit message with:

```text
Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

## Git

- **Never push.** All work stays local until the user explicitly asks.
- Work on the `feat/sync-agent` branch. Do not commit to `main`.

## Environment facts (verified, do not re-derive)

| Fact | Value |
|---|---|
| SQL Server | `.\SQLEXPRESS`, Windows auth, database `AdventureWorks2025` |
| Target framework | `net8.0` (runtime 8.0.14 present; SDK is 9.0.308) |
| Platform API | `http://localhost:5100`, header `X-Api-Key: candidate-test-key-2026` |
| sqlcmd | `C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe` (omit `-E`; trusted auth is the default) |

Package versions are pinned in the `.csproj` files. An unpinned
`dotnet add package` resolves 10.x packages targeting `net10.0` and breaks the
build. Do not "upgrade" them.

The verified database facts, the query designs and the reasoning behind them now
live in `CHALLENGE_SUBMISSION.md`. Read it before changing a query.

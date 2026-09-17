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

<!-- Add new entries above this line as work continues. -->

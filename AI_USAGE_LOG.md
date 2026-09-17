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

<!-- Add new entries above this line as work continues. -->

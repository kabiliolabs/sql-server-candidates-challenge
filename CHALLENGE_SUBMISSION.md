# SyncAgent - Challenge Submission

## Candidate

- **Name: Anderson Lázaro Moreno**
- **Date: 19/03/2026**

---

## How to Run

<!-- Provide step-by-step instructions to build and run your solution -->
1. Run SyncPlatform:
   - Navigate to `src/SyncPlatform`
   - Execute: `dotnet run`
   - The service will start at `http://localhost:5100`

2. Configure SyncAgent:
   - Open `candidate/src/SyncAgent/appsettings.json`
   - Set your SQL Server connection string (AdventureWorks)

3. Run SyncAgent:
   - Execute: `dotnet run --project SyncAgent`

4. Use SyncPlatform UI:
   - Click any of the task buttons (GetCustomers, GetProducts, etc.)
   - Observe results and logs
   
---

## Architecture Decisions

<!-- Explain your project structure and key design decisions. Why did you choose this approach? -->
- Implemented as a **.NET 8 Worker Service** because the problem describes an always-on background process with polling behavior.
- Chose a **single application with internal separation** instead of multiple projects to avoid overengineering.
- Structured by responsibilities:
  - Contracts (API models)
  - Infrastructure (HTTP + SQL)
  - Services (orchestration)
  - TaskHandlers (one per task type)
  - Validation
  - Worker (polling loop)
- Used a **handler-per-task pattern** to ensure extensibility and clean separation of logic.
- Selected **Dapper** for database access to maintain control over SQL and keep the solution lightweight.

---

## Security Measures

<!-- What security measures did you implement and why? -->
- API key is stored in configuration (not hardcoded)
- SQL queries are parameterized to prevent SQL injection
- HTTP client configured with timeout
- Input validation ensures only valid tasks are executed
- No sensitive data committed to repository (connection string uses placeholder)

---

## Testing Strategy

<!-- What did you test and why? What would you test with more time? -->
Focused on testing the most critical logic:

- Task validation (invalid input, unsupported task types)
- Task execution orchestration (handler resolution, result building)

With more time, I would add:
- Integration tests with a real or in-memory database
- HTTP client mocking tests
- End-to-end tests with SyncPlatform
- Edge case coverage (large datasets, failure scenarios)

---

## Known Limitations

<!-- Be honest about trade-offs you made due to time constraints. What would you improve? -->
- No retry or backoff strategy implemented
- No batching or streaming for large datasets
- Limited test coverage (focused on core logic)
- Basic logging (no structured logging/metrics)
- Assumes well-formed input from SyncPlatform

---

## AI Tools Used

<!-- Which AI tools did you use? How did you use them? Be specific.
     Examples:
     - "Used GitHub Copilot for autocomplete while writing query classes"
     - "Used ChatGPT to research AdventureWorks schema relationships"
     - "Used Claude to generate unit test boilerplate" -->
- Used ChatGPT to:
  - refine architecture decisions
  - review code structure and design patterns
  - improve validation and error handling
  - assist in writing unit tests
- Used AI as a support tool, but all decisions and code were manually reviewed and validated

---

## Time Spent

<!-- Approximate breakdown of how you spent your time -->
Total: ~9 hours (spread across implementation, testing, debugging and documentation)

---

## Feedback

<!-- Any feedback on the challenge itself? Was anything unclear? What would you change? -->
- The challenge is well designed and realistic, especially the SyncPlatform simulation.
- Requirements are clear, but:
  - Some expectations (like strict validation or error handling depth) are implicit rather than explicit.
- The exercise is a good balance between coding, architecture, and reasoning.
- Adding explicit expected JSON output examples for each task could make validation easier.
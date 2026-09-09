using System.Runtime.CompilerServices;

// The wire DTOs are internal because nothing outside this assembly should build one: the
// domain model is the type the rest of the application passes around. The contract tests are
// the exception — verifying that the serialised body matches docs/sample-payloads means
// serialising the real DTO, not a hand-written stand-in that could drift away from it and
// keep passing.
[assembly: InternalsVisibleTo("SyncAgent.Core.Tests")]

using System.Runtime.CompilerServices;

// Lets SyncAgent.Worker.Tests call internal members (e.g. Worker.ProcessTaskAsync)
// directly, without making them public just for testability.
[assembly: InternalsVisibleTo("SyncAgent.Worker.Tests")]

using Mediator;

namespace SyncAgent.Core.Queries;

// One IRequest<IReadOnlyList<object>> per task type. The dispatcher (Dispatch/SyncTaskDispatcher.cs)
// still needs a small taskType-string -> query-construction map, since the platform's taskType
// arrives as an untyped runtime string but Mediator dispatch is compile-time typed — that map is
// the one place a new task type costs more than "one line," everything else here is a single new
// record.
public sealed record GetCustomersQuery(DateTimeOffset ModifiedSince, int MaxRows) : IRequest<IReadOnlyList<object>>;

public sealed record GetProductsQuery(DateTimeOffset ModifiedSince, int MaxRows) : IRequest<IReadOnlyList<object>>;

public sealed record GetOrdersQuery(DateTimeOffset ModifiedSince, int MaxRows) : IRequest<IReadOnlyList<object>>;

public sealed record GetProductInventoryQuery(DateTimeOffset ModifiedSince, int MaxRows) : IRequest<IReadOnlyList<object>>;

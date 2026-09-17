using Mediator;
using Microsoft.Extensions.Logging;

namespace SyncAgent.Core.Dispatch;

/// <summary>Wraps every Mediator request with duration logging — a decorator around the
/// handler rather than a logging call embedded inside each one, so handlers stay focused on
/// "fetch and shape data" and this cross-cutting concern lives in exactly one place. Logs
/// metadata only (request type name, record count, elapsed time) — never the request or
/// response payload itself, which would risk logging row-level data.</summary>
public sealed class LoggingPipelineBehavior<TRequest, TResponse>(ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();

        var response = await next(message, cancellationToken);

        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startedAt);
        var recordCount = response is System.Collections.ICollection collection ? collection.Count : (int?)null;

        logger.LogInformation("{RequestName} handled in {ElapsedMs}ms ({RecordCount} records).",
            requestName, elapsed.TotalMilliseconds, recordCount);

        return response;
    }
}

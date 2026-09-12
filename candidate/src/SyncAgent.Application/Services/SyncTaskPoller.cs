using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using SyncAgent.Application.Abstractions;

namespace SyncAgent.Application.Services;

public interface ISyncTaskPoller
{
    Task PollAndExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Runs one poll cycle: ask the platform for work, execute it, report the outcome. Every failure
/// mode is contained here so the hosted service's loop survives to the next cycle.
/// </summary>
public sealed class SyncTaskPoller(
    ISyncPlatformClient client,
    ISyncTaskDispatcher dispatcher,
    ISyncPlatformConnectivityTracker connectivityTracker,
    ILogger<SyncTaskPoller> logger) : ISyncTaskPoller
{
    private const string CompletedStatus = "completed";
    private const string FailedStatus = "failed";

    public async Task PollAndExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var task = await client.GetNextTaskAsync(cancellationToken);

            if (connectivityTracker.RecordSuccess())
            {
                logger.LogInformation("Connection to the sync platform restored");
            }

            if (task is null)
            {
                logger.LogDebug("No tasks available (204 No Content)");
                return;
            }

            logger.LogInformation("Received task {TaskId} of type {TaskType}", task.TaskId, task.TaskType);

            // System.Text.Json does not enforce non-nullable reference types, so "parameters" can
            // arrive null despite the contract. Normalizing lets the handlers report it as a
            // validation error rather than a NullReferenceException.
            var parameters = task.Parameters ?? [];

            var executedAt = DateTime.UtcNow;
            var result = await dispatcher.DispatchAsync(task.TaskType, parameters, cancellationToken);
            var status = result.Success ? CompletedStatus : FailedStatus;

            await client.SubmitResultAsync(
                task.TaskId,
                task.TaskType,
                status,
                result.Data,
                result.RecordCount,
                executedAt,
                result.ErrorMessage,
                cancellationToken);

            logger.LogInformation(
                "Submitted result for task {TaskId}: status={Status}, recordCount={RecordCount}",
                task.TaskId, status, result.RecordCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Poll cycle cancelled");
        }
        catch (OperationCanceledException ex)
        {
            // A cancellation our own token did not request is HttpClient's timeout surfacing as
            // TaskCanceledException: the platform or the database did not answer in time.
            logger.LogWarning("The sync platform did not respond within the configured timeout");
            logger.LogDebug(ex, "Sync platform timeout detail");
        }
        catch (HttpRequestException ex)
        {
            var reason = ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused }
                ? "connection refused - is SyncPlatform running?"
                : ex.Message;

            // A brief outage is expected for an always-on agent and resolves itself, so only the
            // transition into one is worth a warning; while it persists every poll would repeat it.
            var level = connectivityTracker.RecordFailure() ? LogLevel.Warning : LogLevel.Debug;
            logger.Log(level, "Could not reach the sync platform ({Reason}). Will retry on the next poll.", reason);
            logger.LogDebug(ex, "Sync platform connectivity failure detail");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during the poll cycle");
        }
    }
}

using SyncAgent.Application.Abstractions;
using SyncAgent.Domain;

namespace SyncAgent.Application.Services;

/// <summary>
/// Shared pipeline for every sync task: validate the task parameters, query the matching
/// repository, and turn the outcome into the platform's result contract. Concrete handlers only
/// supply their task type and the query-plus-mapping step.
/// </summary>
public abstract class SyncTaskHandlerBase<TRecord> : ISyncTaskHandler
{
    public abstract string TaskType { get; }

    protected abstract Task<IReadOnlyList<TRecord>> FetchRecordsAsync(
        DateTime modifiedSince,
        CancellationToken cancellationToken);

    public async Task<SyncTaskExecutionResult> ExecuteAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var taskParameters = new SyncTaskParameters
            {
                ModifiedSince = parameters.GetValueOrDefault("modifiedSince", string.Empty)
            };

            var records = await FetchRecordsAsync(taskParameters.ParseModifiedSince(), cancellationToken);
            return SyncTaskExecutionResult.Completed(records);
        }
        catch (SyncTaskValidationException ex)
        {
            return SyncTaskExecutionResult.Failed(ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the worker shutting down or a timeout elapsing, not a task that failed.
            // The poller distinguishes the two, so it must reach it instead of being reported as a result.
            throw;
        }
        catch (Exception ex)
        {
            // Only the message travels to the platform; the poller logs the full exception locally so
            // stack traces and other internals stay out of the payload.
            return SyncTaskExecutionResult.Failed(ex.Message);
        }
    }
}

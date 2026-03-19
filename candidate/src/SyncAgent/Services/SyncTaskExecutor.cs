using Microsoft.Extensions.Logging;
using SyncAgent.Contracts;
using SyncAgent.Domain;
using SyncAgent.TaskHandlers;
using SyncAgent.Validation;

namespace SyncAgent.Services;

public sealed class SyncTaskExecutor : ISyncTaskExecutor
{
    private readonly IReadOnlyDictionary<string, ISyncTaskHandler> _handlers;
    private readonly ISyncTaskValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SyncTaskExecutor> _logger;

    public SyncTaskExecutor(
        IEnumerable<ISyncTaskHandler> handlers,
        ISyncTaskValidator validator,
        TimeProvider timeProvider,
        ILogger<SyncTaskExecutor> logger)
    {
        _handlers = handlers.ToDictionary(handler => handler.TaskType, StringComparer.OrdinalIgnoreCase);
        _validator = validator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SyncResultContract> ExecuteAsync(SyncTaskContract task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        var executedAt = _timeProvider.GetUtcNow();
        var validation = _validator.Validate(task);
        if (!validation.IsValid)
        {
            return CreateFailedResult(task, validation.ErrorMessage!, executedAt);
        }

        if (!_handlers.TryGetValue(task.TaskType, out var handler))
        {
            return CreateFailedResult(task, $"No handler registered for task type {task.TaskType}", executedAt);
        }

        try
        {
            var data = await handler.HandleAsync(task, cancellationToken);
            return new SyncResultContract
            {
                TaskId = task.TaskId,
                TaskType = task.TaskType,
                Status = "completed",
                Data = data,
                RecordCount = data.Count,
                ExecutedAt = executedAt,
                ErrorMessage = null
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Task {TaskId} failed during execution.", task.TaskId);
            return CreateFailedResult(task, exception.Message, executedAt);
        }
    }

    private static SyncResultContract CreateFailedResult(SyncTaskContract task, string errorMessage, DateTimeOffset executedAt)
        => new()
        {
            TaskId = task.TaskId,
            TaskType = task.TaskType,
            Status = "failed",
            Data = null,
            RecordCount = 0,
            ExecutedAt = executedAt,
            ErrorMessage = errorMessage
        };
}

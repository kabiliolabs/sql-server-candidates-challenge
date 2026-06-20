using SyncTaskWorkerService.Models;
using System.Text.Json;

namespace SyncTaskWorkerService.SyncAgentTask
{
    public class SyncTaskDispatcher
    {
        private readonly IEnumerable<ISyncTaskHandler> _syncTasksHandler;
        public SyncTaskDispatcher(IEnumerable<ISyncTaskHandler> syncTaskHandlers)
        {
            _syncTasksHandler = syncTaskHandlers;
        }

        public async Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken ct)
        {   
            var handler = _syncTasksHandler.FirstOrDefault(h => h.SyncName == task.TaskType);

            if (handler is null)
                return new SyncResult
                {
                    TaskId = task.TaskId,
                    TaskType = task.TaskType,
                    Status = "failed",
                    ExecutedAt = DateTime.UtcNow,
                    ErrorMessage = $"No handler found for task type: {task.TaskType}"
                };

            var data = await handler.HandleAsync(JsonSerializer.SerializeToElement(task.Parameters), ct);
           
            return new SyncResult
            {
                TaskId = task.TaskId,
                TaskType = task.TaskType,
                Status = "completed",
                ExecutedAt = DateTime.UtcNow,
                RecordCount = data.Count(),
                Data = data.ToList()
            };
        }
}
}

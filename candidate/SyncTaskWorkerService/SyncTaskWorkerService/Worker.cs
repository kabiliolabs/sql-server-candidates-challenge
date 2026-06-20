using SyncTaskWorkerService.CentralPlatformHttpClient;
using SyncTaskWorkerService.SyncAgentTask;

namespace SyncTaskWorkerService
{
    public class Worker(ILogger<Worker> logger, 
                        IConfiguration configuration, 
                        IPlatformHttpClient platformHttpClient,
                        SyncTaskDispatcher syncTaskDispatcher) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var syncTask = await platformHttpClient.GetTaskAsync(ct);

                if (syncTask is not null)
                {
                    logger.LogInformation("Task receive : {TaskType}", syncTask.TaskType);

                    //trigger dispatcher to process the task X
                    var result = await syncTaskDispatcher.DispatchAsync(syncTask, ct);

                    //post result
                    var postResult = await platformHttpClient.PostResultAsync(result, ct);

                    logger.LogInformation("Task {TaskType} with Id {TaskId} processed with POST status {Status}", result.TaskType, result.TaskId, postResult);

                    continue; 
                }

                await Task.Delay(configuration.GetValue<int>("PollingTime"), ct);
            }   
        }
    }
}

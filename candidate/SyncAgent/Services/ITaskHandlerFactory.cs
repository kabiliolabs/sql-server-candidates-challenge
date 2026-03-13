namespace SyncAgent.Services;

public interface ITaskHandlerFactory
{
    ITaskHandler GetHandler(string taskType);
    bool HasHandler(string taskType);
}

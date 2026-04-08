using System.Collections.Concurrent;
using SyncPlatform.Models;

namespace SyncPlatform.Services;

public class TaskQueueService
{
    private readonly ConcurrentQueue<SyncTask> _queue = new();

    public void Enqueue(SyncTask task)
    {
        _queue.Enqueue(task);
    }

    public bool TryDequeue(out SyncTask? task)
    {
        return _queue.TryDequeue(out task);
    }

    public int Count => _queue.Count;

    public SyncTask CreateGetCustomersTask()
    {
        return new SyncTask
        {
            TaskId = Ulid.NewUlid().ToString(),
            TaskType = "GetCustomers",
            Parameters = new Dictionary<string, string>
            {
                ["modifiedSince"] = "2002-01-01T00:00:00Z"
            },
            CreatedAt = DateTime.UtcNow
        };
    }

    public SyncTask CreateGetProductsTask()
    {
        return new SyncTask
        {
            TaskId = Ulid.NewUlid().ToString(),
            TaskType = "GetProducts",
            Parameters = new Dictionary<string, string>
            {
                ["modifiedSince"] = "2002-01-01T00:00:00Z"
            },
            CreatedAt = DateTime.UtcNow
        };
    }

    public SyncTask CreateGetOrdersTask()
    {
        return new SyncTask
        {
            TaskId = Ulid.NewUlid().ToString(),
            TaskType = "GetOrders",
            Parameters = new Dictionary<string, string>
            {
                ["modifiedSince"] = "2002-01-01T00:00:00Z"
            },
            CreatedAt = DateTime.UtcNow
        };
    }

    public SyncTask CreateGetProductInventoryTask()
    {
        return new SyncTask
        {
            TaskId = Ulid.NewUlid().ToString(),
            TaskType = "GetProductInventory",
            Parameters = new Dictionary<string, string>
            {
                ["modifiedSince"] = "2002-01-01T00:00:00Z"
            },
            CreatedAt = DateTime.UtcNow
        };
    }
}

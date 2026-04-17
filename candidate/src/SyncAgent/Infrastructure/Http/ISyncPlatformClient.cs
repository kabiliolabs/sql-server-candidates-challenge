using SyncAgent.Contracts;

namespace SyncAgent.Infrastructure.Http;

public interface ISyncPlatformClient
{
    Task<SyncTaskContract?> GetNextTaskAsync(CancellationToken cancellationToken);
    Task SubmitResultAsync(SyncResultContract result, CancellationToken cancellationToken);
}

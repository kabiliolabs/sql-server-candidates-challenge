using SyncAgent.Domain.Models;

namespace SyncAgent.Application.Abstractions;

public interface IInventoryRepository
{
    Task<List<InventoryItem>> GetInventoryAsync(DateTime modifiedSince, CancellationToken cancellationToken);
}

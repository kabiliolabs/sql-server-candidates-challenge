using SyncAgent.Domain.Models;

namespace SyncAgent.Application.Abstractions;

public interface IOrderRepository
{
    Task<List<Order>> GetOrdersAsync(DateTime modifiedSince, CancellationToken cancellationToken);
}

using SyncAgent.Domain.Models;

namespace SyncAgent.Application.Abstractions;

public interface ICustomerRepository
{
    Task<List<Customer>> GetCustomersAsync(DateTime modifiedSince, CancellationToken cancellationToken);
}

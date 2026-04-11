using SyncAgent.Data;
using SyncAgent.Models;
using SyncAgent.Services;

namespace SyncAgent.Handlers;

public class GetCustomersHandler : ITaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public GetCustomersHandler(IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public string TaskType => "GetCustomers";

    public async Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken = default)
    {
        var modifiedSince = task.GetModifiedSince();
        var customers = await _repository.GetCustomersAsync(modifiedSince, cancellationToken);

        var data = customers.Select(c => (object)new
        {
            customerId = c.CustomerId,
            accountNumber = c.AccountNumber,
            firstName = c.FirstName,
            lastName = c.LastName,
            emailAddress = c.EmailAddress,
            phone = c.Phone,
            addressLine1 = c.AddressLine1,
            city = c.City,
            stateProvince = c.StateProvince,
            postalCode = c.PostalCode,
            countryRegion = c.CountryRegion
        }).ToList();

        return SyncResult.Success(task.TaskId, task.TaskType, data);
    }
}

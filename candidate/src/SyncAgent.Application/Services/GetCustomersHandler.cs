using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Contracts;

namespace SyncAgent.Application.Services;

public sealed class GetCustomersHandler(ICustomerRepository repository) : SyncTaskHandlerBase<CustomerRecord>
{
    public override string TaskType => "GetCustomers";

    protected override async Task<IReadOnlyList<CustomerRecord>> FetchRecordsAsync(
        DateTime modifiedSince,
        CancellationToken cancellationToken)
    {
        var customers = await repository.GetCustomersAsync(modifiedSince, cancellationToken);

        return customers.Select(customer => new CustomerRecord
        {
            CustomerId = customer.CustomerId,
            AccountNumber = customer.AccountNumber,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            EmailAddress = customer.EmailAddress,
            Phone = customer.Phone,
            AddressLine1 = customer.AddressLine1,
            City = customer.City,
            StateProvince = customer.StateProvince,
            PostalCode = customer.PostalCode,
            CountryRegion = customer.CountryRegion
        }).ToList();
    }
}

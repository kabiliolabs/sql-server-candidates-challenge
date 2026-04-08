
using SyncPlatformClient.Domain.Contracts.Customers;
using SyncPlatformClient.Domain.DTOs;
using SyncPlatformClient.Repository.DataModels;
using SyncPlatformClient.Repository.Repository.Interfaces;
using System.Threading.Tasks;

namespace SyncPlatformClient.Domain.Services.Customer
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;

        

        public CustomerService(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public async Task<SyncResult> GetCustomerListAsync(SyncTask task, CancellationToken cancellationToken = default)
        {
            var modifiedSinceDate = task.Parameters.TryGetValue("modifiedSince", out var date);
            if(modifiedSinceDate)
            {
                var modifiedSince = DateTimeHelper.GetModifiedDate(date);
                var customerList = await _customerRepository.GetCustomerListAsync(modifiedSince, cancellationToken);
                var data = customerList.Select(c => (object)new
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

                return SyncResult.ToSyncResult(task.TaskId, task.TaskType, data);
            }
            return new SyncResult();
        }
    }
}

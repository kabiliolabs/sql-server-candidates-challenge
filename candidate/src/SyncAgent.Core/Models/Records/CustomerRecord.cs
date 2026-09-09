namespace SyncAgent.Core.Models.Records
{
    /// <summary>
    /// One row of a <c>GetCustomers</c> payload.
    /// </summary>
    /// <remarks>
    /// Property names are PascalCase and carry no serialization attributes. The camelCase the
    /// platform requires is applied centrally by the serializer settings in Infrastructure,
    /// which keeps the wire format a single decision instead of one repeated on every
    /// property of every record type.
    /// </remarks>
    public sealed class CustomerRecord
    {
        public int CustomerId { get; init; }

        public string AccountNumber { get; init; } = string.Empty;

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public string? EmailAddress { get; init; }

        public string? Phone { get; init; }

        public string? AddressLine1 { get; init; }

        public string? City { get; init; }

        public string? StateProvince { get; init; }

        public string? PostalCode { get; init; }

        public string? CountryRegion { get; init; }
    }
}

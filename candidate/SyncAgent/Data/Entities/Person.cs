namespace SyncAgent.Data.Entities;

public class Person
{
    public int BusinessEntityId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public List<EmailAddress> EmailAddresses { get; set; } = new();
    public List<PersonPhone> PersonPhones { get; set; } = new();
    public List<BusinessEntityAddress> BusinessEntityAddresses { get; set; } = new();
}

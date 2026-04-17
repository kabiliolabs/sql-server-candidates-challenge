namespace SyncAgent.Domain;

public static class SyncTaskType
{
    public const string GetCustomers = "GetCustomers";
    public const string GetProducts = "GetProducts";
    public const string GetOrders = "GetOrders";
    public const string GetProductInventory = "GetProductInventory";

    public static readonly ISet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        GetCustomers,
        GetProducts,
        GetOrders,
        GetProductInventory
    };
}

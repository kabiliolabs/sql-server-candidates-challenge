namespace SyncAgent.Core.Tasks;

/// <summary>The allow-listed set of task types the platform can request. A taskType string
/// that doesn't match one of these must never reach the dispatcher or a SQL query.</summary>
public static class TaskTypes
{
    public const string GetCustomers = "GetCustomers";
    public const string GetProducts = "GetProducts";
    public const string GetOrders = "GetOrders";
    public const string GetProductInventory = "GetProductInventory";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        GetCustomers, GetProducts, GetOrders, GetProductInventory
    };
}

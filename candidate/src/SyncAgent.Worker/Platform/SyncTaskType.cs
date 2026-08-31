using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Platform;

/// <summary>
/// The four task types the platform can request, per docs/api-contract.md.
/// [JsonConverter(JsonStringEnumConverter)] lets these deserialize directly from the
/// JSON string values ("GetCustomers", etc.), which already match the member names.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SyncTaskType
{
    GetCustomers,
    GetProducts,
    GetOrders,
    GetProductInventory
}

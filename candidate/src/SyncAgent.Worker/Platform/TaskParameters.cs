using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Platform;

public record TaskParameters
{
    [JsonPropertyName("modifiedSince")]
    public DateTimeOffset? ModifiedSince { get; init; }
}

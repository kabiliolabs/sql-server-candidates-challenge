using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SyncAgent.Infrastructure.Serialization
{
    /// <summary>
    /// The single JSON configuration used for everything the agent sends and receives.
    /// </summary>
    /// <remarks>
    /// Getting this wrong is the sharpest failure mode in the whole contract, and it fails
    /// silently. The platform deserialises the result body with
    /// <c>JsonSerializer.Deserialize&lt;SyncResult&gt;(body)</c> and no options, so
    /// <c>PropertyNameCaseInsensitive</c> is false while every property on its model carries
    /// an explicit lowercase <c>[JsonPropertyName]</c>. A body serialised with default .NET
    /// naming therefore binds nothing at all: deserialisation succeeds, every string stays
    /// empty, and the agent gets back "400 Missing required field: taskId" — an error that
    /// points at the wrong side of the wire.
    ///
    /// So camelCase is applied here, once, by contract resolver rather than by attributes on
    /// every property of every record. The trade-off is that the wire format stops being
    /// visible at the model; the contract test in SyncAgent.Core.Tests pins it instead, by
    /// asserting the exact key set against docs/sample-payloads.
    ///
    /// Newtonsoft rather than System.Text.Json: on net48 STJ pulls in System.Memory,
    /// System.Runtime.CompilerServices.Unsafe and System.Text.Encodings.Web, each needing a
    /// binding redirect, whereas Newtonsoft has no dependencies at all on this TFM.
    /// </remarks>
    public static class SyncJson
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),

            // ISO-8601 with a trailing Z. Every DateTime the agent emits is already tagged
            // Utc, and DateTimeZoneHandling.Utc is what turns that into the "Z" suffix the
            // contract shows rather than a "+00:00" offset.
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,

            // The contract's own example sends "errorMessage": null on a completed result,
            // so nulls are emitted rather than dropped.
            NullValueHandling = NullValueHandling.Include,

            // Read money as decimal, never double. Binary floating point cannot represent
            // 1431.50 exactly, and a sync that shifts prices in the last cents is a defect
            // that surfaces in someone's reconciliation months later.
            FloatParseHandling = FloatParseHandling.Decimal,

            // Unknown properties from the platform are ignored rather than fatal: a platform
            // that adds a field must not take every deployed agent offline.
            MissingMemberHandling = MissingMemberHandling.Ignore,

            Formatting = Formatting.None,
        };

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(SerializerSettings);

        /// <summary>Serialises a payload for transmission to the platform.</summary>
        public static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, SerializerSettings);
        }

        /// <summary>
        /// Deserialises a body received from the platform.
        /// </summary>
        /// <exception cref="JsonException">The body is not valid JSON for the target type.</exception>
        public static T? Deserialize<T>(string json)
            where T : class
        {
            return JsonConvert.DeserializeObject<T>(json, SerializerSettings);
        }

        /// <summary>The shared serializer, for callers that need to write straight to a stream.</summary>
        public static JsonSerializer Instance => Serializer;
    }
}

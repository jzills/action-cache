using Newtonsoft.Json;
using ActionCache.Common.Serialization;

namespace ActionCache.Common.Serialization;

/// <summary>
/// Serializes cache-key components.
/// </summary>
/// <remarks>
/// Still Newtonsoft with <c>TypeNameHandling.Auto</c>, because reflection-based refresh
/// reverses a key back into typed action arguments and needs the type information to do
/// it. That reverse path — and this serializer with it — is removed once refresh replays
/// real requests instead. Until then the binder below remains the mitigation here, while
/// cached <em>values</em> no longer carry type information at all.
/// </remarks>
internal static class KeyComponentSerializer
{
    /// <summary>
    /// The settings used for cache-key components.
    /// </summary>
    internal static readonly JsonSerializerSettings SerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        SerializationBinder = new SafeSerializationBinder(),
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = new List<JsonConverter> { new ActionArgumentsConverter() }
    };

    /// <summary>
    /// Serializes a key component to JSON.
    /// </summary>
    /// <typeparam name="T">The type of the component.</typeparam>
    /// <param name="obj">The component to serialize. Can be null.</param>
    /// <returns>A JSON string representation of the component.</returns>
    internal static string Serialize<T>(T? obj) =>
        JsonConvert.SerializeObject(obj, typeof(T), SerializerSettings);

    /// <summary>
    /// Deserializes a key component from JSON.
    /// </summary>
    /// <typeparam name="T">The type of the component.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized component, or <see langword="null"/>.</returns>
    internal static T? Deserialize<T>(string json) =>
        JsonConvert.DeserializeObject<T>(json, SerializerSettings);
}

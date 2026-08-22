using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ActionCache.Common.Serialization;

/// <summary>
/// Serializes the values ActionCache stores in a backend.
/// </summary>
/// <remarks>
/// System.Text.Json with no polymorphism. Earlier versions used Newtonsoft with
/// <c>TypeNameHandling.Auto</c> and a deny-list binder, so a cache entry could name the
/// type to instantiate on read; responses are now stored as a
/// <see cref="ActionCache.Common.Responses.CachedResponse"/> of primitives, and nothing
/// in a cached payload can influence which types are constructed.
/// </remarks>
internal static class CacheJsonSerializer
{
    /// <summary>
    /// The options used for every value written to and read from a backend. The
    /// source-generated context is tried first and reflection fills in for the
    /// caller-supplied types that reach <see cref="ActionCache.IActionCache"/> directly.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            CacheJsonSerializerContext.Default,
            new DefaultJsonTypeInfoResolver()),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a value to JSON.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="obj">The value to serialize. Can be null.</param>
    /// <returns>A JSON string representation of the value.</returns>
    internal static string Serialize<T>(T? obj) =>
        JsonSerializer.Serialize(obj, typeof(T), SerializerOptions);

    /// <summary>
    /// Deserializes a JSON string.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized value, or <see langword="null"/> if it could not be read.</returns>
    internal static T? Deserialize<T>(string json) =>
        (T?)JsonSerializer.Deserialize(json, typeof(T), SerializerOptions);
}

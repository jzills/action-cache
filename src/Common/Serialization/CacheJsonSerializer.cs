using Newtonsoft.Json;

namespace ActionCache.Common.Serialization;

/// <summary>
/// Provides methods for serializing and deserializing JSON data with custom settings.
/// </summary>
internal static class CacheJsonSerializer
{
    /// <summary>
    /// Gets the JSON serializer settings used for serialization and deserialization.
    /// This includes settings for handling type names, avoiding reference loops, 
    /// and using custom converters.
    /// </summary>
    internal static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = new List<JsonConverter> { new ActionArgumentsConverter() }
    };

    /// <summary>
    /// Serializes an object to a JSON string using the predefined serializer settings.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize. Can be null.</param>
    /// <returns>A JSON string representation of the object.</returns>
    internal static string Serialize<T>(T? obj) => 
        JsonConvert.SerializeObject(obj, SerializerSettings);

    /// <summary>
    /// Deserializes a JSON string to an object of type <typeparamref name="T"/> 
    /// using the predefined serializer settings.
    /// </summary>
    /// <typeparam name="T">The type to which the JSON string is deserialized.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>An object of type <typeparamref name="T"/>, or null if deserialization fails.</returns>
    internal static T? Deserialize<T>(string json) => 
        JsonConvert.DeserializeObject<T>(json, SerializerSettings);
}
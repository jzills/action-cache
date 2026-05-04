using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ActionCache.Common.Serialization;

/// <summary>
/// Converts JSON data to and from a <see cref="Dictionary{TKey, TValue}"/> 
/// where the keys are strings and values are objects, using specific rules 
/// to handle different JSON token types.
/// </summary>
internal class ActionArgumentsConverter : JsonConverter<Dictionary<string, object>>
{
    /// <summary>
    /// Reads JSON and converts it into a <see cref="Dictionary{TKey, TValue}"/>
    /// with string keys and object values, deserializing each property based on its type.
    /// </summary>
    /// <param name="reader">The JSON reader to read from.</param>
    /// <param name="objectType">The type of the object to read into (not used here).</param>
    /// <param name="existingValue">The existing value of the dictionary (if any).</param>
    /// <param name="hasExistingValue">A boolean indicating if there is an existing value.</param>
    /// <param name="serializer">The serializer instance to use for nested objects.</param>
    /// <returns>A <see cref="Dictionary{TKey, TValue}"/> with deserialized data from JSON.</returns>
    public override Dictionary<string, object> ReadJson(
        JsonReader reader, 
        Type objectType, 
        Dictionary<string, object>? existingValue, 
        bool hasExistingValue, 
        JsonSerializer serializer
    )
    {
        var result = new Dictionary<string, object>();

        var obj = JObject.Load(reader);
        foreach (var property in obj.Properties())
        {
            var value = ConvertToken(property.Value, serializer);
            result.Add(property.Name, value);
        }

        return result;
    }

    /// <summary>
    /// Converts a JSON token to a CLR object, handling various JSON types 
    /// including strings, integers, floats, booleans, objects, arrays, and nulls.
    /// </summary>
    /// <param name="token">The JSON token to convert.</param>
    /// <param name="serializer">The serializer instance for nested objects.</param>
    /// <returns>The converted CLR object.</returns>
    private object? ConvertToken(JToken token, JsonSerializer serializer) =>
        token.Type switch
        {
            JTokenType.String   => ConvertStringToken(token),
            JTokenType.Integer  => token.ToObject<long>(),
            JTokenType.Float    => token.ToObject<decimal>(),
            JTokenType.Boolean  => token.ToObject<bool>(),
            JTokenType.Null     => null,
            JTokenType.Object   => token.ToObject<object>(serializer),
            JTokenType.Array    => token.ToObject<object[]>(serializer),
            _                   => token.ToObject<object>(serializer)
        };

    /// <summary>
    /// Converts a JSON token representing a string to either a GUID, DateTime, or plain string, 
    /// based on the content of the token.
    /// </summary>
    /// <param name="token">The JSON token to convert.</param>
    /// <returns>The converted value as either a GUID, DateTime, or string.</returns>
    private object ConvertStringToken(JToken token)
    {
        var value = token.ToString();

        if (Guid.TryParse(value, out var guidValue))
        {
            return guidValue;
        }
        else if (DateTime.TryParse(value, out var dateTimeValue))
        {
            return dateTimeValue;
        }
        else
        {
            return value;
        }
    }

    /// <summary>
    /// Writes a <see cref="Dictionary{TKey, TValue}"/> to JSON, where each entry's key 
    /// and value are serialized into a JSON object.
    /// </summary>
    /// <param name="writer">The JSON writer to write to.</param>
    /// <param name="source">The dictionary to serialize. Can be null.</param>
    /// <param name="serializer">The serializer instance for nested objects.</param>
    public override void WriteJson(
        JsonWriter writer, 
        Dictionary<string, object>? source, 
        JsonSerializer serializer
    )
    {
        writer.WriteStartObject();

        if (source is not null)
        {
            foreach (var (key, value) in source)
            {
                writer.WritePropertyName(key);
                serializer.Serialize(writer, value, typeof(object));
            }
        }

        writer.WriteEndObject();
    }
}
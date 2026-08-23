using ActionCache.Common.Serialization;
using System.Collections.Specialized;

namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for working with <see cref="NameValueCollection"/> objects.
/// </summary>
internal static class NameValueCollectionExtensions
{
    /// <summary>
    /// Attempts to retrieve a value from the <see cref="NameValueCollection"/> by key and deserialize it from JSON into the specified type.
    /// </summary>
    /// <typeparam name="T">The type to which the JSON value should be deserialized.</typeparam>
    /// <param name="nameValues">The collection of name-value pairs to search.</param>
    /// <param name="name">The key associated with the JSON value to be retrieved and deserialized.</param>
    /// <returns>
    /// An instance of type <typeparamref name="T"/> if deserialization succeeds; otherwise, <c>null</c>.
    /// </returns>
    internal static T? ParseValueAsJson<T>(this NameValueCollection nameValues, string name) where T : class
    {
        var json = nameValues.Get(name);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return KeyComponentSerializer.Deserialize<T>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
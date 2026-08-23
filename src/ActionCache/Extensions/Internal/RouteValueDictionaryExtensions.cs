using Microsoft.AspNetCore.Routing;

namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="RouteValueDictionary"/> to simplify working with route values.
/// </summary>
internal static class RouteValueDictionaryExtensions
{
    /// <summary>
    /// Attempts to retrieve a string value from the specified <see cref="RouteValueDictionary"/> by key.
    /// </summary>
    /// <param name="source">The <see cref="RouteValueDictionary"/> to retrieve the value from.</param>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">
    /// When this method returns, contains the string value associated with the specified key, if found;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the specified key exists and the value is not <c>null</c>; otherwise, <c>false</c>.
    /// </returns>
    internal static bool TryGetStringValue(this RouteValueDictionary source, string key, out string? value)
    {
        if (source.ContainsKey(key))
        {
            value = (string?)source[key];
            return value is not null;
        }
        else
        {
            value = null;
            return false;
        }
    }
}
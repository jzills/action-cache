using ActionCache.Common.Enums;
using Microsoft.AspNetCore.Http;

namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="IHeaderDictionary"/> to add cache-related headers.
/// </summary>
internal static class IHeaderDictionaryExtensions
{
    /// <summary>
    /// Adds a cache status header to the <see cref="IHeaderDictionary"/> if it does not already exist.
    /// </summary>
    /// <param name="headers">The header dictionary to which the cache status will be added.</param>
    /// <param name="status">The <see cref="CacheStatus"/> value to be added as the cache status.</param>
    internal static void AddCacheStatus(
        this IHeaderDictionary headers, 
        CacheStatus status
    ) => headers.TryAdd(CacheHeaders.CacheStatus, Enum.GetName(status));
}
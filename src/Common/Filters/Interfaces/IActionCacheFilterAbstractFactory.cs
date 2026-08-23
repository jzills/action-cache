using ActionCache.Common.Enums;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActionCache.Common.Filters;

/// <summary>
/// An interface representing an abstract factory for cache filter creation.
/// </summary>
public interface IActionCacheFilterAbstractFactory<T>
{
    /// <summary>
    /// Creates an instance of a cache filter.
    /// </summary>
    /// <param name="namespace">The namespace for the caches used in the filter.</param>
    /// <param name="type">The filter type to create.</param>
    /// <returns>An implementation of IFilterMetadata.</returns>
    T CreateInstance(Namespace @namespace, FilterType type);

    /// <summary>
    /// Creates an instance of a cache filter.
    /// </summary>
    /// <param name="namespace">The namespace for the caches used in the filter.</param>
    /// <param name="absoluteExpiration">The absolute expiration in milliseconds for a cache entry.</param>
    /// <param name="slidingExpiration">The sliding expiration in milliseconds for a cache entry.</param>  
    /// /// <param name="type">The filter type to create.</param>
    /// <returns>An implementation of IFilterMetadata.</returns>
    T CreateInstance(Namespace @namespace, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration, FilterType type);

    /// <summary>
    /// Creates an instance of a cache filter.
    /// </summary>
    /// <param name="namespace">The namespace for the caches used in the filter.</param>
    /// <param name="absoluteExpiration">The absolute expiration in milliseconds for a cache entry.</param>
    /// <param name="slidingExpiration">The sliding expiration in milliseconds for a cache entry.</param>
    /// <param name="type">The filter type to create.</param>
    /// <param name="singleFlight">Whether concurrent misses for one key are coalesced so the action runs once.</param>
    /// <returns>An implementation of the filter abstraction.</returns>
    T CreateInstance(Namespace @namespace, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration, FilterType type, bool singleFlight);
}

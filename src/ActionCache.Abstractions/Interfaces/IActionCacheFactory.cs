using ActionCache.Utilities;

namespace ActionCache;

/// <summary>
/// Provides functionalities to create specific instances of IActionCache based on a specified type.
/// </summary>
public interface IActionCacheFactory
{
    /// <summary>
    /// Creates an action cache instance with a specified namespace.
    /// </summary>
    /// <param name="namespace">The namespace to associate with the cache.</param>
    /// <returns>An instance of IActionCache or null if it cannot be created.</returns>
    IActionCache? Create(Namespace @namespace);

    /// <summary>
    /// Creates an action cache instance with a specified namespace and entry options with specified absolute and sliding expirations.
    /// </summary>
    /// <param name="namespace">The namespace to associate with the cache.</param>
    /// <param name="absoluteExpiration">The absolute expiration for cache entry options.</param>
    /// <param name="slidingExpiration">The sliding expiration for cache entry options.</param> 
    /// <returns>An instance of IActionCache or null if it cannot be created.</returns>
    IActionCache? Create(Namespace @namespace, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);
}
using System.Collections.Concurrent;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace ActionCache.Memory.Extensions.Internal;

/// <summary>
/// Provides extension methods for working with <see cref="IMemoryCache"/>.
/// </summary>
/// <remarks>
/// These methods are not internally synchronized. Callers must hold the namespace's lock —
/// see <see cref="ActionCache.Memory.MemoryActionCache"/>, which wraps every call in its
/// cache locker. <c>GetOrCreate</c> is not atomic, so without
/// that lock two concurrent writers can each build an index and lose one another's keys.
/// </remarks>
internal static class IMemoryCacheExtensions
{
    /// <summary>
    /// Retrieves the index of cached keys for a namespace, creating it when absent and
    /// sweeping out entries whose absolute expiration has passed.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="namespace">The namespace whose key index is read.</param>
    /// <param name="indexOptions">The entry options used when the index is written back. These describe the index entry itself, never a cached response.</param>
    /// <returns>A dictionary of cache keys mapped to their absolute expiration, if any.</returns>
    internal static ConcurrentDictionary<string, DateTimeOffset?> GetKeys(
        this IMemoryCache cache,
        Namespace @namespace,
        MemoryCacheEntryOptions indexOptions
    )
    {
        var indexKey = CreateIndexKey(@namespace);

        if (!cache.TryGetValue<ConcurrentDictionary<string, DateTimeOffset?>>(indexKey, out var keys) || keys is null)
        {
            keys = new ConcurrentDictionary<string, DateTimeOffset?>();
            cache.Set(indexKey, keys, indexOptions);
            return keys;
        }

        var expired = keys.Where(key => key.Value.HasValue && DateTimeOffset.UtcNow >= key.Value.Value).ToList();
        if (expired.Count > 0)
        {
            foreach (var entry in expired)
            {
                keys.TryRemove(entry.Key, out _);
            }

            cache.Set(indexKey, keys, indexOptions);
        }

        return keys;
    }

    /// <summary>
    /// Adds a key to a namespace's index.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="namespace">The namespace the key belongs to.</param>
    /// <param name="key">The cache key to record.</param>
    /// <param name="absoluteExpiration">The cached response's absolute expiration, used to sweep the index later. <see langword="null"/> for entries that do not expire.</param>
    /// <param name="indexOptions">The entry options used when the index is written back.</param>
    internal static void SetKey(
        this IMemoryCache cache,
        Namespace @namespace,
        string key,
        DateTimeOffset? absoluteExpiration,
        MemoryCacheEntryOptions indexOptions
    )
    {
        var keys = cache.GetKeys(@namespace, indexOptions);
        keys[key] = absoluteExpiration;
        cache.Set(CreateIndexKey(@namespace), keys, indexOptions);

    }

    /// <summary>
    /// Removes a key from a namespace's index.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="namespace">The namespace the key belongs to.</param>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="indexOptions">The entry options used when the index is written back.</param>
    internal static void RemoveKey(
        this IMemoryCache cache,
        Namespace @namespace,
        string key,
        MemoryCacheEntryOptions indexOptions
    )
    {
        var keys = cache.GetKeys(@namespace, indexOptions);
        if (keys.TryRemove(key, out _))
        {
            cache.Set(CreateIndexKey(@namespace), keys, indexOptions);
        }
    }

    /// <summary>
    /// Builds the cache key under which a namespace's key index is stored.
    /// </summary>
    /// <param name="namespace">The namespace whose index key is built.</param>
    /// <returns>The index's cache key.</returns>
    /// <remarks>
    /// Derived from the <em>resolved</em> namespace string, so a route-templated namespace
    /// such as <c>Account:{id}</c> gets one index per id rather than one shared across all
    /// of them. The suffix keeps it clear of two neighbours that share that string: the
    /// <see cref="ActionCache.Memory.ExpirationTokenSources"/> entry, which stores the
    /// namespace's <see cref="CancellationTokenSource"/> under the bare namespace, and
    /// cached responses, which are stored under a hex-encoded key.
    /// </remarks>
    private static string CreateIndexKey(Namespace @namespace) => $"{(string)@namespace}:__keys";
}

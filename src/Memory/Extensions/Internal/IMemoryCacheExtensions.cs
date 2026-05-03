using System.Collections.Concurrent;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace ActionCache.Memory.Extensions.Internal;

/// <summary>
/// Provides extension methods for working with <see cref="IMemoryCache"/>.
/// </summary>
internal static class IMemoryCacheExtensions
{
    /// <summary>
    /// Retrieves a dictionary of cached keys associated with the specified namespace.
    /// If no keys exist, a new dictionary is created and stored in the cache.
    /// Expired keys are removed before returning the result.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="namespace">The namespace used to store and retrieve keys.</param>
    /// <param name="entryOptions">The cache entry options used when storing updated keys.</param>
    /// <returns>A <see cref="ConcurrentDictionary{TKey, TValue}"/> containing cached keys and their expiration times.</returns>
    internal static ConcurrentDictionary<string, DateTimeOffset?> GetKeys(this IMemoryCache cache, Namespace @namespace, MemoryCacheEntryOptions entryOptions)
    {
        var keys = cache.GetOrCreate(@namespace, options =>
        {
            options.Size = 1;
            return new ConcurrentDictionary<string, DateTimeOffset?>();
        });

        if (keys is null)
        {
            cache.Remove(@namespace);
        }
        else
        {
            var entries = keys.Where(key => DateTimeOffset.UtcNow >= key.Value);
            if (entries.Any())
            {
                foreach (var entry in entries)
                {
                    keys.TryRemove(entry.Key, out _);
                }

                cache.Set(@namespace, keys, entryOptions);
            }
        }

        return keys ?? [];
    }

    /// <summary>
    /// Adds a key to the cache under the specified namespace with the provided expiration options.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="namespace">The namespace under which the key is stored.</param>
    /// <param name="key">The key to add to the cache.</param>
    /// <param name="entryOptions">The cache entry options specifying expiration and other settings.</param>
    internal static void SetKey(this IMemoryCache cache, Namespace @namespace, string key, MemoryCacheEntryOptions entryOptions)
    {
        var keys = cache.GetKeys(@namespace, entryOptions);
        if (keys.TryAdd(key, entryOptions.AbsoluteExpiration))
        {
            cache.Set(@namespace, keys, entryOptions);
        }
    }

    /// <summary>
    /// Removes a specified key from the cache under the given namespace.
    /// If the key exists, it is removed, and the updated dictionary is stored back in the cache.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="namespace">The namespace containing the key to remove.</param>
    /// <param name="key">The key to remove from the cache.</param>
    /// <param name="entryOptions">The cache entry options used to update the stored dictionary.</param>
    internal static void RemoveKey(this IMemoryCache cache, Namespace @namespace, string key, MemoryCacheEntryOptions entryOptions)
    {
        var keys = cache.GetKeys(@namespace, entryOptions);
        if (keys.Any())
        {
            if (keys.TryRemove(key, out _))
            {
                cache.Set(@namespace, keys, entryOptions);
            }
        }
    }
}
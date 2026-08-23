using ActionCache.Common.Serialization;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Concurrent;

namespace ActionCache.Memory.Extensions.Internal;

/// <summary>
/// Provides extension methods for managing cached keys in an <see cref="IDistributedCache"/> instance.
/// </summary>
internal static class IDistributedCacheExtensions
{
    /// <summary>
    /// Retrieves a dictionary of cached keys associated with the specified namespace.
    /// If no keys exist, an empty dictionary is returned.
    /// Expired keys are removed before returning the result.
    /// </summary>
    /// <param name="cache">The distributed cache instance.</param>
    /// <param name="namespace">The namespace used to store and retrieve keys.</param>
    /// <param name="entryOptions">The cache entry options used when storing updated keys.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning a <see cref="ConcurrentDictionary{TKey, TValue}"/> 
    /// containing cached keys and their expiration times.
    /// </returns>
    internal static async Task<ConcurrentDictionary<string, DateTimeOffset?>> GetKeysAsync(this IDistributedCache cache, Namespace @namespace, DistributedCacheEntryOptions entryOptions)
    {
        var json = cache.GetString(@namespace);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new();
        }
        else
        {
            var keys = CacheJsonSerializer.Deserialize<ConcurrentDictionary<string, DateTimeOffset?>>(json);
            if (keys is null)
            {
                await cache.RemoveAsync(@namespace);
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

                    await cache.SetStringAsync(@namespace, CacheJsonSerializer.Serialize(keys), entryOptions);
                }
            }

            return keys ?? [];
        }
    }

    /// <summary>
    /// Adds a key to the cache under the specified namespace with the provided expiration options.
    /// </summary>
    /// <param name="cache">The distributed cache instance.</param>
    /// <param name="namespace">The namespace under which the key is stored.</param>
    /// <param name="key">The key to add to the cache.</param>
    /// <param name="entryOptions">The cache entry options specifying expiration and other settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async Task SetKeyAsync(this IDistributedCache cache, Namespace @namespace, string key, DistributedCacheEntryOptions entryOptions)
    {
        var keys = await cache.GetKeysAsync(@namespace, entryOptions);
        if (keys.TryAdd(key, entryOptions.AbsoluteExpiration))
        {
            await cache.SetStringAsync(@namespace, CacheJsonSerializer.Serialize(keys), entryOptions);
        }
    }

    /// <summary>
    /// Removes a specified key from the cache under the given namespace.
    /// If the key exists, it is removed, and the updated dictionary is stored back in the cache.
    /// </summary>
    /// <param name="cache">The distributed cache instance.</param>
    /// <param name="namespace">The namespace containing the key to remove.</param>
    /// <param name="key">The key to remove from the cache.</param>
    /// <param name="entryOptions">The cache entry options used to update the stored dictionary.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async Task RemoveKeyAsync(this IDistributedCache cache, Namespace @namespace, string key, DistributedCacheEntryOptions entryOptions)
    {
        var keys = await cache.GetKeysAsync(@namespace, entryOptions);
        if (keys.Any())
        {
            if (keys.TryRemove(key, out _))
            {
                await cache.SetStringAsync(@namespace, CacheJsonSerializer.Serialize(keys), entryOptions);
            }
        }
    }
}
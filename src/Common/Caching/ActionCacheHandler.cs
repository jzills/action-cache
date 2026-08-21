using ActionCache.Utilities;

namespace ActionCache.Common.Caching;

/// <summary>
/// Provides an implementation of <see cref="IActionCacheHandler"/> that handles action caching with support for chaining multiple caches.
/// </summary>
public class ActionCacheHandler : ActionCacheHandlerBase, IActionCache
{
    /// <summary>
    /// The primary <see cref="IActionCache"/> instance used for caching operations.
    /// </summary>
    protected readonly IActionCache Cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheHandler"/> class with the specified cache instance.
    /// </summary>
    /// <param name="cache">The <see cref="IActionCache"/> instance used for caching operations.</param>
    public ActionCacheHandler(IActionCache cache) => Cache = cache;

    /// <summary>
    /// Retrieves an item by key, falling through to the next cache in the chain and promoting
    /// what it finds into this one.
    /// </summary>
    /// <typeparam name="TValue">The type of the cached value.</typeparam>
    /// <param name="key">The key of the cached item.</param>
    /// <returns>The cached value if found; otherwise, <c>null</c>.</returns>
    public async Task<TValue?> GetAsync<TValue>(string key)
    {
        var value = await Cache.GetAsync<TValue?>(key);
        if (value is not null)
        {
            return value;
        }

        var next = await NextIfExists(cache => cache.GetAsync<TValue?>(key));
        if (next is not null)
        {
            // Promote so later reads are served by the faster layer. The copy expires on
            // this layer's schedule, which may be shorter than the authoritative layer's —
            // that is the intended relationship: L1 caches L2, it does not replicate it.
            await Cache.SetAsync(key, next);
        }

        return next;
    }

    /// <summary>
    /// Retrieves every cache key across all layers of the chain.
    /// </summary>
    /// <returns>The union of the keys held by each layer.</returns>
    public async Task<IEnumerable<string>> GetKeysAsync()
    {
        var keys = await Cache.GetKeysAsync() ?? [];
        var next = await NextIfExists(cache => cache.GetKeysAsync()) ?? [];

        // Union, not Concat: the same key normally lives in several layers, and callers use
        // this to drive per-key removal and refresh — duplicates mean duplicated work, and
        // for refresh, duplicated action invocations.
        return keys.Union(next);
    }

    /// <summary>
    /// Gets the namespace associated with this cache.
    /// </summary>
    /// <returns>The cache namespace.</returns>
    public Namespace GetNamespace() => Cache.GetNamespace();

    /// <summary>
    /// Refreshes the cache, potentially updating or reloading cached entries. Also refreshes the next cache in the chain, if it exists.
    /// </summary>
    /// <returns>A task that represents the asynchronous refresh operation.</returns>
    public async Task RefreshAsync()
    {
        await Cache.RefreshAsync();
        await NextIfExists(next => next.RefreshAsync());
    }

    /// <summary>
    /// Removes a specific item from the cache by key. Also removes the item from the next cache in the chain, if it exists.
    /// </summary>
    /// <param name="key">The key of the item to remove.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    public async Task RemoveAsync(string key)
    {
        await Cache.RemoveAsync(key);
        await NextIfExists(next => next.RemoveAsync(key));
    }

    /// <summary>
    /// Removes all items from the cache. Also removes all items from the next cache in the chain, if it exists.
    /// </summary>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    public async Task RemoveAsync()
    {
        await Cache.RemoveAsync();
        await NextIfExists(next => next.RemoveAsync());
    }

    /// <summary>
    /// Sets a value in the cache with the specified key. Also sets the value in the next cache in the chain, if it exists.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to cache.</typeparam>
    /// <param name="key">The key for the cached value.</param>
    /// <param name="value">The value to cache.</param>
    /// <returns>A task that represents the asynchronous set operation.</returns>
    public async Task SetAsync<TValue>(string key, TValue? value)
    {
        await Cache.SetAsync(key, value);
        await NextIfExists(next => next.SetAsync(key, value));
    }
}
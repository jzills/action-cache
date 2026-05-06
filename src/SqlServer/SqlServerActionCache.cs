using System.Diagnostics.CodeAnalysis;
using ActionCache.Common.Caching;
using ActionCache.SqlServer.Concurrency.Locks;
using ActionCache.Common.Serialization;
using ActionCache.Memory.Extensions.Internal;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Distributed;

namespace ActionCache.SqlServer;

/// <summary>
/// A cache implementation for SQL Server-based action caching with distributed locking support.
/// </summary>
public class SqlServerActionCache : ActionCacheBase<SqlServerCacheLock>
{
    /// <summary>
    /// The distributed cache used for storing and retrieving cache entries.
    /// </summary>
    protected readonly IDistributedCache Cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerActionCache"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache to be used.</param>
    /// <param name="context">The cache context.</param>  
    public SqlServerActionCache(IDistributedCache cache, ActionCacheContext<SqlServerCacheLock> context) 
        : base(context) => Cache = cache;

    /// <summary>
    /// Gets or sets the cache entry options that control the expiration and sliding expiration of cache items.
    /// </summary>
    /// <summary>
    /// Creates the entry options for memory cache.
    /// </summary>
    /// <value>The cache entry options applied to new entries.</value>
    private DistributedCacheEntryOptions CreateEntryOptions() =>
        new DistributedCacheEntryOptions
        {
            SlidingExpiration = EntryOptions.SlidingExpiration,
            AbsoluteExpiration = EntryOptions.GetAbsoluteExpirationFromUtcNow()
        };

    /// <summary>
    /// Asynchronously gets a value from the cache.
    /// </summary>
    /// <typeparam name="TValue">The type of the cached value.</typeparam>
    /// <param name="key">The key of the cache entry.</param>
    /// <returns>The cached value or the default value of the type if not found.</returns>
#pragma warning disable CS8609
    public override async Task<TValue> GetAsync<TValue>(string key)
    {
        var json = await Cache.GetStringAsync(Namespace.Create(key));
        if (string.IsNullOrWhiteSpace(json))
        {
            return await Task.FromResult<TValue>(default!);
        }
        else
        {
            return CacheJsonSerializer.Deserialize<TValue>(json)!;
        }
    }
#pragma warning restore CS8609

    /// <summary>
    /// Asynchronously sets a value in the cache.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to set in the cache.</typeparam>
    /// <param name="key">The cache key to set the value for.</param>
    /// <param name="value">The value to set in the cache.</param>
    public override async Task SetAsync<TValue>(string key, [AllowNull] TValue value)
    {
        var entryOptions = CreateEntryOptions();
        await Cache.SetStringAsync(Namespace.Create(key), CacheJsonSerializer.Serialize(value), entryOptions);
        
        await CacheLocker.WaitForLockThenAsync(Namespace, 
            () => Cache.SetKeyAsync(Namespace, key, entryOptions));
    }

    /// <summary>
    /// Asynchronously removes a value from the cache.
    /// </summary>
    /// <param name="key">The key of the cache entry to remove.</param>
    public override async Task RemoveAsync(string key)
    {
        await Cache.RemoveAsync(Namespace.Create(key));

        await CacheLocker.WaitForLockThenAsync(Namespace,
            () => Cache.RemoveKeyAsync(Namespace, key, CreateEntryOptions()));
    }

    /// <summary>
    /// Asynchronously removes all values from the cache.
    /// </summary>
    public override async Task RemoveAsync()
    {
        var keys = await GetKeysAsync();
        await Task.WhenAll(keys.Select(RemoveAsync));
    }

    /// <summary>
    /// Retrieves all keys associated with this cache.
    /// </summary>
    /// <returns>An enumerable of strings representing current cache entry keys.</returns>
    public override async Task<IEnumerable<string>> GetKeysAsync()
    {
        var keysWithAbsoluteExpiration = await CacheLocker.WaitForLockThenAsync(Namespace,
            () => Cache.GetKeysAsync(Namespace, CreateEntryOptions()));

        return keysWithAbsoluteExpiration?.Keys.AsEnumerable() ?? [];
    }
}
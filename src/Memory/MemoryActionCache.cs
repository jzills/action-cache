using System.Diagnostics.CodeAnalysis;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency.Locks;
using ActionCache.Memory.Extensions.Internal;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace ActionCache.Memory;

/// <summary>
/// Represents a memory action cache implementation.
/// </summary>
public class MemoryActionCache : ActionCacheBase<NullCacheLock>
{
    /// <summary>
    /// A memory cache implementation.
    /// </summary>
    protected readonly IMemoryCache Cache;

    /// <summary>
    /// A source of tokens used to combine cache entries for actions like namespace eviction.
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryActionCache"/> class.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="cancellationTokenSource">The source for cancellation tokens used to expire cache entries.</param>
    /// <param name="context">The cache context.</param>  
    public MemoryActionCache(
        IMemoryCache cache,
        CancellationTokenSource cancellationTokenSource,
        ActionCacheContext<NullCacheLock> context
    ) : base(context)
    {
        Cache = cache;
        CancellationTokenSource = cancellationTokenSource;
    }

    /// <summary>
    /// Creates the entry options for memory cache.
    /// </summary>
    /// <value>The cache entry options applied to new entries.</value>
    private MemoryCacheEntryOptions CreateEntryOptions() =>
        new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = EntryOptions.SlidingExpiration,
            AbsoluteExpiration = EntryOptions.GetAbsoluteExpirationFromUtcNow()
        }.AddExpirationToken(new CancellationChangeToken(CancellationTokenSource.Token));

    /// <summary>
    /// Asynchronously gets a value from the cache.
    /// </summary>
    /// <param name="key">The key of the cache entry.</param>
    /// <returns>The cached value or null if not found.</returns> 
#pragma warning disable CS8609, CS8619
    public override Task<TValue> GetAsync<TValue>(string key) =>
        Task.FromResult(Cache.Get<TValue>(Namespace.Create(key)));
#pragma warning restore CS8609, CS8619

    /// <summary>
    /// Asynchronously sets a value in the cache.
    /// </summary>
    /// <param name="key">The cache key to set the value for.</param>
    /// <param name="value">The value to set in the cache.</param>
    public override Task SetAsync<TValue>(string key, [AllowNull] TValue value)
    {
        var entryOptions = CreateEntryOptions();
        Cache.Set(Namespace.Create(key), value, entryOptions);

        return CacheLocker.WaitForLockThenAsync(Namespace,
            () => Cache.SetKey(Namespace, key, entryOptions));
    }

    /// <summary>
    /// Asynchronously removes a value from the cache.
    /// </summary>
    /// <param name="key">The key of the cache entry to remove.</param>
    public override Task RemoveAsync(string key)
    {
        Cache.Remove(Namespace.Create(key));

        return CacheLocker.WaitForLockThenAsync(Namespace, 
            () => Cache.RemoveKey(Namespace, key, CreateEntryOptions()));
    }

    /// <summary>
    /// Asynchronously removes all values from the cache.
    /// </summary>
    public override async Task RemoveAsync()
    {
        await CancellationTokenSource.CancelAsync();
        CancellationTokenSource.Dispose();
        CancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Retrieves all keys associated with this cache.
    /// </summary>
    /// <returns>An enumerable of strings representing current cache entry keys.</returns>
    public override async Task<IEnumerable<string>> GetKeysAsync()
    {
        var keysWithAbsoluteExpiration = await CacheLocker.WaitForLockThenAsync(Namespace,
            () => Cache.GetKeys(Namespace, CreateEntryOptions()));

        return keysWithAbsoluteExpiration?.Keys.AsEnumerable() ?? [];
    }
}
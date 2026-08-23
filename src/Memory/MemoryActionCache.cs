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
public class MemoryActionCache : ActionCacheBase<SemaphoreSlimLock>
{
    /// <summary>
    /// A memory cache implementation.
    /// </summary>
    protected readonly IMemoryCache Cache;

    /// <summary>
    /// The source of the namespace's expiration token. Resolved per operation so this cache
    /// can never write entries against a token that a prior eviction already cancelled.
    /// </summary>
    protected readonly IExpirationTokenSources ExpirationTokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryActionCache"/> class.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="expirationTokens">The source of the namespace's expiration token.</param>
    /// <param name="context">The cache context.</param>  
    public MemoryActionCache(
        IMemoryCache cache,
        IExpirationTokenSources expirationTokens,
        ActionCacheContext<SemaphoreSlimLock> context
    ) : base(context)
    {
        Cache = cache;
        ExpirationTokens = expirationTokens;
    }

    /// <summary>
    /// Resolves the namespace's current expiration token.
    /// </summary>
    /// <returns>A change token tied to the namespace's live <see cref="CancellationTokenSource"/>.</returns>
    private CancellationChangeToken CreateExpirationToken()
    {
        ExpirationTokens.TryGetOrAdd(Namespace, out var cancellationTokenSource);
        return new CancellationChangeToken(cancellationTokenSource.Token);
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
        }.AddExpirationToken(CreateExpirationToken());

    /// <summary>
    /// Creates the entry options used for the namespace's key index. The index is owned by
    /// the namespace lifecycle, so it carries the namespace expiration token but never a
    /// caller's absolute or sliding expiration.
    /// </summary>
    /// <returns>The cache entry options applied to the namespace key index.</returns>
    private MemoryCacheEntryOptions CreateIndexOptions() =>
        new MemoryCacheEntryOptions { Size = 1 }
            .AddExpirationToken(CreateExpirationToken());

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
            () => Cache.SetKey(Namespace, key, EntryOptions.GetAbsoluteExpirationFromUtcNow(), CreateIndexOptions()));
    }

    /// <summary>
    /// Asynchronously removes a value from the cache.
    /// </summary>
    /// <param name="key">The key of the cache entry to remove.</param>
    public override Task RemoveAsync(string key)
    {
        Cache.Remove(Namespace.Create(key));

        return CacheLocker.WaitForLockThenAsync(Namespace, 
            () => Cache.RemoveKey(Namespace, key, CreateIndexOptions()));
    }

    /// <summary>
    /// Asynchronously removes all values from the cache.
    /// </summary>
    public override Task RemoveAsync()
    {
        // Cancelling the namespace's token source evicts every entry carrying it, the key
        // index included. Lifecycle lives in the token source, which owns the store.
        ExpirationTokens.Reset(Namespace);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves all keys associated with this cache.
    /// </summary>
    /// <returns>An enumerable of strings representing current cache entry keys.</returns>
    public override async Task<IEnumerable<string>> GetKeysAsync()
    {
        var keysWithAbsoluteExpiration = await CacheLocker.WaitForLockThenAsync(Namespace,
            () => Cache.GetKeys(Namespace, CreateIndexOptions()));

        return keysWithAbsoluteExpiration?.Keys.AsEnumerable() ?? [];
    }
}
using System.Diagnostics.CodeAnalysis;
using ActionCache.Common;
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
    /// Creates the distributed cache entry options from the current <see cref="ActionCacheBase{TLock}.EntryOptions"/>.
    /// </summary>
    /// <returns>A <see cref="DistributedCacheEntryOptions"/> configured with the current sliding and absolute expiration values.</returns>
    private DistributedCacheEntryOptions CreateEntryOptions() => CreateEntryOptions(EntryOptions);

    /// <summary>
    /// Creates the distributed cache entry options for the given expirations.
    /// </summary>
    /// <param name="options">The expirations to apply.</param>
    /// <returns>The options to write the entry and its key-index record with.</returns>
    private static DistributedCacheEntryOptions CreateEntryOptions(ActionCacheEntryOptions options) =>
        new()
        {
            SlidingExpiration = options.SlidingExpiration,
            AbsoluteExpiration = options.GetAbsoluteExpirationFromUtcNow()
        };

    /// <summary>
    /// Asynchronously gets a value from the cache.
    /// </summary>
    /// <typeparam name="TValue">The type of the cached value.</typeparam>
    /// <param name="key">The key of the cache entry.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The cached value or the default value of the type if not found.</returns>
#pragma warning disable CS8609
    public override async Task<TValue> GetAsync<TValue>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await Cache.GetStringAsync(Namespace.Create(key), cancellationToken);
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
    /// <param name="entryOptions">The expirations to write with, or <see langword="null"/> for this cache's own.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public override async Task SetAsync<TValue>(string key, [AllowNull] TValue value, ActionCacheEntryOptions? entryOptions, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The key-index record takes the same expiration as the entry, so a refreshed entry
        // and its index record continue to expire together.
        var distributedOptions = CreateEntryOptions(EffectiveEntryOptions(entryOptions));
        await Cache.SetStringAsync(Namespace.Create(key), CacheJsonSerializer.Serialize(value), distributedOptions, cancellationToken);

        await CacheLocker.WaitForLockThenAsync(Namespace,
            () => Cache.SetKeyAsync(Namespace, key, distributedOptions));
    }

    /// <summary>
    /// Asynchronously removes a value from the cache.
    /// </summary>
    /// <param name="key">The key of the cache entry to remove.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public override async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Cache.RemoveAsync(Namespace.Create(key), cancellationToken);

        await CacheLocker.WaitForLockThenAsync(Namespace,
            () => Cache.RemoveKeyAsync(Namespace, key, CreateEntryOptions()));
    }

    /// <summary>
    /// Asynchronously removes all values from the cache.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public override async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = await GetKeysAsync(cancellationToken);
        await Task.WhenAll(keys.Select(key => RemoveAsync(key, cancellationToken)));
    }

    /// <summary>
    /// Retrieves all keys associated with this cache.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>An enumerable of strings representing current cache entry keys.</returns>
    public override async Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keysWithAbsoluteExpiration = await CacheLocker.WaitForLockThenAsync(Namespace,
            () => Cache.GetKeysAsync(Namespace, CreateEntryOptions()));

        return keysWithAbsoluteExpiration?.Keys.AsEnumerable() ?? [];
    }
}
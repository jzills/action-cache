using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;
using ActionCache.Utilities;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ActionCache.Redis;

/// <summary>
/// Factory class for creating RedisActionCache instances.
/// </summary>
public class RedisActionCacheFactory : ActionCacheFactoryBase
{
    /// <summary>
    /// An IDatabase representation of a Redis cache.
    /// </summary>
    protected readonly IDatabase Cache;

    /// <summary>
    /// Constructor for RedisActionCacheFactory.
    /// </summary>
    /// <param name="connectionMultiplexer">ConnectionMultiplexer for Redis.</param>
    /// <param name="entryOptions">The global entry options used for creation when expiration times are not supplied.</param>
    /// <param name="refreshProvider">The refresh provider to handle cache refreshes.</param>
    public RedisActionCacheFactory(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<ActionCacheEntryOptions> entryOptions,
        IActionCacheRefreshProvider refreshProvider
    ) : base(entryOptions, refreshProvider)
    {
        Cache = connectionMultiplexer.GetDatabase();
    }

    /// <inheritdoc/>
    public override IActionCache? Create(Namespace @namespace)
    {
        var context = new ActionCacheContext<NullCacheLock>
        {
            Namespace = @namespace,
            EntryOptions = EntryOptions,
            RefreshProvider = RefreshProvider,
            CacheLocker = new NullCacheLocker()
        };

        return new RedisActionCache(Cache, context);
    }

    /// <inheritdoc/>
    public override IActionCache? Create(Namespace @namespace, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        var context = new ActionCacheContext<NullCacheLock>
        {
            Namespace = @namespace,
            EntryOptions = new ActionCacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration,
                SlidingExpiration = slidingExpiration,
                LockDuration = EntryOptions.LockDuration,
                LockTimeout = EntryOptions.LockTimeout
            },
            RefreshProvider = RefreshProvider,
            CacheLocker = new NullCacheLocker()
        };

        return new RedisActionCache(Cache, context);
    }
}

using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ActionCache.Redis;

/// <summary>
/// Factory class for creating RedisActionCache instances.
/// </summary>
public class RedisActionCacheFactory : ActionCacheFactoryBase
{
    /// <summary>
    /// The Redis database instance used for cache read and write operations.
    /// </summary>
    protected readonly IDatabase Cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisActionCacheFactory"/> class.
    /// </summary>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer used to obtain the database instance.</param>
    /// <param name="entryOptions">The global entry options used for creation when expiration times are not supplied.</param>
    /// <param name="refreshProvider">The refresh provider to handle cache refreshes.</param>
    /// <param name="loggerFactory">The factory used to create the logger for this cache factory.</param>
    public RedisActionCacheFactory(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<ActionCacheEntryOptions> entryOptions,
        IActionCacheRefreshProvider refreshProvider,
        ILoggerFactory loggerFactory
    ) : base(entryOptions, refreshProvider, loggerFactory)
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
                Logger = Logger,
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
                LockTimeout = EntryOptions.LockTimeout
            },
            RefreshProvider = RefreshProvider,
                Logger = Logger,
            CacheLocker = new NullCacheLocker()
        };

        return new RedisActionCache(Cache, context);
    }
}

using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionCache.Memory;

/// <summary>
/// Represents a factory for creating memory action caches.
/// </summary>
public class MemoryActionCacheFactory : ActionCacheFactoryBase
{
    /// <summary>
    /// A memory cache implementation.
    /// </summary>
    protected readonly IMemoryCache Cache;

    /// <summary>
    /// A source of expiration tokens.
    /// </summary>
    protected readonly IExpirationTokenSources ExpirationTokens;

    /// <summary>
    /// The shared, process-wide locker guarding each namespace's key index.
    /// </summary>
    protected readonly SemaphoreSlimCacheLocker Locker;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryActionCacheFactory"/> class.
    /// </summary>
    /// <param name="cache">The memory cache to use.</param>
    /// <param name="expirationTokens">The expiration token source to use.</param>
    /// <param name="entryOptions">The global entry options used for creation when expiration times are not supplied.</param>
    /// <param name="refreshProvider">The refresh provider responsible for invoking cached controller actions.</param>
    /// <param name="loggerFactory">The factory used to create the logger for this cache factory.</param>
    /// <param name="locker">The shared locker guarding each namespace's key index. Must be the process-wide singleton: caches are created per request, so a locker created here would guard nothing.</param>
    public MemoryActionCacheFactory(
        IMemoryCache cache,
        IExpirationTokenSources expirationTokens,
        IOptions<ActionCacheEntryOptions> entryOptions,
        IActionCacheRefreshProvider refreshProvider,
        ILoggerFactory loggerFactory,
        SemaphoreSlimCacheLocker locker
    ) : base(entryOptions, refreshProvider, loggerFactory)
    {
        Cache = cache;
        ExpirationTokens = expirationTokens;
        Locker = locker;
    }

    /// <inheritdoc/>
    public override IActionCache? Create(Namespace @namespace)
    {
        if (ExpirationTokens.TryGetOrAdd(@namespace, out var expirationTokenSource))
        {
            var context = new ActionCacheContext<SemaphoreSlimLock>
            {
                Namespace = @namespace,
                EntryOptions = EntryOptions,
                RefreshProvider = RefreshProvider,
                CacheLocker = Locker
            };

            return new MemoryActionCache(Cache, expirationTokenSource, context);
        }
        else
        {
            LogCreationFailed(@namespace);
            return default;
        }
    }

    /// <inheritdoc/>
    public override IActionCache? Create(Namespace @namespace, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        if (ExpirationTokens.TryGetOrAdd(@namespace, out var expirationTokenSource))
        {
            var context = new ActionCacheContext<SemaphoreSlimLock>
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
                CacheLocker = Locker
            };

            return new MemoryActionCache(Cache, expirationTokenSource, context);
        }
        else
        {
            LogCreationFailed(@namespace);
            return default;
        }
    }
}
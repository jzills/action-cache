using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;
using ActionCache.Utilities;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ActionCache.AzureCosmos;

/// <summary>
/// Represents a factory for creating Azure Cosmos Db action caches.
/// </summary>
public class AzureCosmosActionCacheFactory : ActionCacheFactoryBase
{
    /// <summary>
    /// The Azure Cosmos DB container used as the backing store for created cache instances.
    /// </summary>
    protected readonly Container Cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCosmosActionCacheFactory"/> class.
    /// </summary>
    /// <param name="cache">The Azure Cosmos Db container to use.</param>
    /// <param name="entryOptions">The global entry options used for creation when expiration times are not supplied.</param>
    /// <param name="refreshProvider">The refresh provider responsible for invoking cached controller actions.</param> 
    public AzureCosmosActionCacheFactory(
        Container cache,
        IOptions<ActionCacheEntryOptions> entryOptions,
        IActionCacheRefreshProvider refreshProvider
    ) : base(entryOptions, refreshProvider)
    {
        Cache = cache;
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

        return new AzureCosmosActionCache(Cache, context);
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
                SlidingExpiration = slidingExpiration
            },
            RefreshProvider = RefreshProvider,
            CacheLocker = new NullCacheLocker()
        };

        return new AzureCosmosActionCache(Cache, context);
    }
}
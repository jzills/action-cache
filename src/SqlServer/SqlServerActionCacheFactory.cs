using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.SqlServer.Concurrency;
using ActionCache.SqlServer.Concurrency.Locks;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Options;

namespace ActionCache.SqlServer;

/// <summary>
/// Factory for creating <see cref="SqlServerActionCache"/> instances backed by
/// <see cref="SqlServerCacheLocker"/> (sp_getapplock / sp_releaseapplock).
/// </summary>
public class SqlServerActionCacheFactory : ActionCacheFactoryBase
{
    /// <summary>The distributed cache used for storing cache entries.</summary>
    protected readonly IDistributedCache Cache;

    /// <summary>Connection string extracted from <see cref="SqlServerCacheOptions"/> for the locker.</summary>
    protected readonly string ConnectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerActionCacheFactory"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache to be used.</param>
    /// <param name="sqlServerCacheOptions">SQL Server cache options; the connection string is used by the locker.</param>
    /// <param name="entryOptions">Global entry options used when expiration is not specified per namespace.</param>
    /// <param name="refreshProvider">Provider responsible for refreshing cached action results.</param>
    public SqlServerActionCacheFactory(
        IDistributedCache cache,
        IOptions<SqlServerCacheOptions> sqlServerCacheOptions,
        IOptions<ActionCacheEntryOptions> entryOptions,
        IActionCacheRefreshProvider refreshProvider
    ) : base(entryOptions, refreshProvider)
    {
        Cache = cache;
        ConnectionString = sqlServerCacheOptions.Value.ConnectionString
            ?? throw new InvalidOperationException(
                "SqlServerCacheOptions.ConnectionString must be set to use SqlServerCacheLocker.");
    }

    /// <inheritdoc/>
    public override IActionCache? Create(Namespace @namespace)
    {
        var context = new ActionCacheContext<SqlServerCacheLock>
        {
            Namespace = @namespace,
            EntryOptions = EntryOptions,
            RefreshProvider = RefreshProvider,
            CacheLocker = new SqlServerCacheLocker(
                ConnectionString,
                EntryOptions.LockDuration,
                EntryOptions.LockTimeout)
        };

        return new SqlServerActionCache(Cache, context);
    }

    /// <inheritdoc/>
    public override IActionCache? Create(Namespace @namespace, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        var context = new ActionCacheContext<SqlServerCacheLock>
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
            CacheLocker = new SqlServerCacheLocker(
                ConnectionString,
                EntryOptions.LockDuration,
                EntryOptions.LockTimeout)
        };

        return new SqlServerActionCache(Cache, context);
    }
}

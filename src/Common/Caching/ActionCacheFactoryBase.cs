using ActionCache.Common.Diagnostics;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionCache.Common.Caching;

/// <summary>
/// A base class for cache factories.
/// </summary>
/// <remarks>
/// Caches produced by <see cref="Create(Namespace)"/>/<see cref="Create(Namespace, TimeSpan?, TimeSpan?)"/>
/// are not wrapped in <see cref="ResilientActionCache"/> when this factory is resolved directly (e.g. by a
/// consumer injecting <see cref="IActionCacheFactory"/> outside the filter pipeline). That wrapping — and the
/// per-operation degradation logging it provides — is applied only by the filter abstract factories
/// (<c>ActionCacheFilterAbstractFactoryBase</c>) via <see cref="ResilientCacheDecorator"/>. Consumers using this
/// factory directly still receive a log entry if cache creation itself fails.
/// </remarks>
public abstract class ActionCacheFactoryBase : IActionCacheFactory
{
    /// <summary>
    /// An instance of global entry options.
    /// </summary>
    protected readonly ActionCacheEntryOptions EntryOptions;

    /// <summary>
    /// An instance of a refresh provider responsible for invoking cached controller actions.
    /// </summary>
    protected readonly IActionCacheRefreshProvider RefreshProvider;

    /// <summary>
    /// The logger used to record cache-creation failures.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheFactoryBase"/> class.
    /// </summary>
    /// <param name="entryOptionsAccessor">Accessor for the globally configured <see cref="ActionCacheEntryOptions"/>.</param>
    /// <param name="refreshProvider">The provider used to refresh stale cache entries by re-invoking their originating actions.</param>
    /// <param name="loggerFactory">The factory used to create the logger for this cache factory.</param>
    public ActionCacheFactoryBase(
        IOptions<ActionCacheEntryOptions> entryOptionsAccessor,
        IActionCacheRefreshProvider refreshProvider,
        ILoggerFactory loggerFactory
    )
    {
        EntryOptions = entryOptionsAccessor.Value;
        RefreshProvider = refreshProvider;
        Logger = loggerFactory.CreateLogger(GetType());
    }

    /// <summary>
    /// Creates an action cache for the specified namespace.
    /// </summary>
    /// <param name="namespace">The namespace for the cache.</param>
    /// <returns>A new action cache if successful, otherwise null.</returns>
    public abstract IActionCache? Create(Namespace @namespace);

    /// <summary>
    /// Creates an action cache for the specified namespace.
    /// </summary>
    /// <param name="namespace">The namespace for the cache.</param>
    /// <param name="absoluteExpiration">The absolute expiration used for entries on this cache.</param>
    /// <param name="slidingExpiration">The sliding expiration used for entries on this cache.</param>
    /// /// <returns>A new action cache if successful, otherwise null.</returns>
    public abstract IActionCache? Create(Namespace @namespace, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration);

    /// <summary>
    /// Logs a cache-creation failure for the specified namespace.
    /// </summary>
    /// <param name="namespace">The namespace creation was attempted for.</param>
    protected void LogCreationFailed(Namespace @namespace) =>
        ActionCacheLog.CacheCreationFailed(Logger, GetType().Name, (string)@namespace);
}
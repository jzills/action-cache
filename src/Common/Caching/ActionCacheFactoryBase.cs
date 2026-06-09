using ActionCache.Utilities;
using Microsoft.Extensions.Options;

namespace ActionCache.Common.Caching;

/// <summary>
/// A base class for cache factories.
/// </summary>
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
    /// Initializes a new instance of the <see cref="ActionCacheFactoryBase"/> class.
    /// </summary>
    /// <param name="entryOptionsAccessor">Accessor for the globally configured <see cref="ActionCacheEntryOptions"/>.</param>
    /// <param name="refreshProvider">The provider used to refresh stale cache entries by re-invoking their originating actions.</param>
    public ActionCacheFactoryBase(
        IOptions<ActionCacheEntryOptions> entryOptionsAccessor,
        IActionCacheRefreshProvider refreshProvider
    )
    {
        EntryOptions = entryOptionsAccessor.Value;
        RefreshProvider = refreshProvider;
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
}
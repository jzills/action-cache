using ActionCache.Common.Concurrency;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Caching;

/// <summary>
/// Represents the context for an action cache, containing configuration and dependencies.
/// </summary>
/// <typeparam name="TLock">The type of cache lock used for synchronization.</typeparam>
public class ActionCacheContext<TLock> where TLock : CacheLock
{
    /// <summary>
    /// Gets or sets the namespace used for cache isolation.
    /// </summary>
    public required Namespace Namespace { get; init; }

    /// <summary>
    /// Gets or sets the cache entry options, including expiration and eviction policies.
    /// </summary>
    public required ActionCacheEntryOptions EntryOptions { get; init; }

    /// <summary>
    /// Gets or sets the refresh provider responsible for handling cache refresh operations.
    /// </summary>
    public required IActionCacheRefreshProvider RefreshProvider { get; init; }

    /// <summary>
    /// Gets or sets the cache locker used for synchronizing access to cached items.
    /// </summary>
    public required ICacheLocker<TLock> CacheLocker { get; init; }

    /// <summary>
    /// Gets or sets the logger used to record refresh outcomes.
    /// </summary>
    public ILogger Logger { get; init; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
}
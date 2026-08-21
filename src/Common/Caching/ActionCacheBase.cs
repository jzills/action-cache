using ActionCache.Common.Concurrency;
using ActionCache.Common.Diagnostics;
using ActionCache.Common.Responses;
using Microsoft.Extensions.Logging;
using ActionCache.Utilities;

namespace ActionCache.Common.Caching;

/// <summary>
/// An abstract class implementation of <see cref="IActionCache"/> 
/// </summary>
public abstract class ActionCacheBase<TLock> : IActionCache where TLock : CacheLock
{
    /// <summary>
    /// The namespace used for cache entries.
    /// </summary>
    protected readonly Namespace Namespace;
    
    /// <summary>
    /// The global entry options used for creation when expiration times are not supplied.
    /// </summary> 
    protected readonly ActionCacheEntryOptions EntryOptions;

    /// <summary>
    /// The refresh provider to handle cache refreshes.
    /// </summary>
    protected readonly IActionCacheRefreshProvider RefreshProvider;

    /// <summary>
    /// The cache locker handling operations with race conditions.
    /// </summary>
    protected readonly ICacheLocker<TLock> CacheLocker;

    /// <summary>
    /// The logger used to record refresh outcomes.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheBase{TLock}"/> class.
    /// </summary>
    /// <param name="context">The context containing necessary dependencies for cache operations.</param>
    public ActionCacheBase(ActionCacheContext<TLock> context)
    {
        Namespace = context.Namespace;
        EntryOptions = context.EntryOptions;
        RefreshProvider = context.RefreshProvider;
        CacheLocker = context.CacheLocker;
        Logger = context.Logger;
    }

    /// <inheritdoc/>
    public abstract Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public Namespace GetNamespace() => Namespace;

    /// <inheritdoc/>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var namespaceValue = (string)Namespace;
        var keys = await GetKeysAsync(cancellationToken);
        var refreshed = 0;
        var requested = 0;

        foreach (var key in keys)
        {
            requested++;
            cancellationToken.ThrowIfCancellationRequested();

            var entry = await GetAsync<CachedResponse>(key, cancellationToken);
            if (entry is null)
            {
                continue;
            }

            if (entry.VariesByRequest)
            {
                // Replaying another caller's request would mean impersonating them.
                ActionCacheLog.RefreshKeySkippedVaryBy(Logger, key, namespaceValue);
                continue;
            }

            if (entry.Request is null)
            {
                ActionCacheLog.RefreshKeySkipped(Logger, key, namespaceValue, "no request was recorded for it");
                continue;
            }

            var replayed = await RefreshProvider.ReplayAsync(entry.Request, cancellationToken);
            if (replayed is null)
            {
                continue;
            }

            await SetAsync(key, replayed, cancellationToken);
            refreshed++;
        }

        ActionCacheLog.RefreshSummary(Logger, namespaceValue, refreshed, requested);
    }

    /// <inheritdoc/>
    public abstract Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task RemoveAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default);
}
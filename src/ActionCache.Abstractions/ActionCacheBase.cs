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

            // One entry must not be able to end the pass. A replay executes the endpoint,
            // so an action that throws — for a resource deleted since it was cached, say —
            // propagates straight out of here, and every remaining key in the namespace
            // silently goes unrefreshed. Failures are per key; only the caller cancelling
            // stops the loop.
            try
            {
                if (await TryRefreshKeyAsync(key, namespaceValue, cancellationToken))
                {
                    refreshed++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ActionCacheLog.RefreshKeyFailed(Logger, exception, key, namespaceValue);
            }
        }

        ActionCacheLog.RefreshSummary(Logger, namespaceValue, refreshed, requested);
    }

    /// <summary>
    /// Refreshes a single cache entry.
    /// </summary>
    /// <param name="key">The cache key to refresh.</param>
    /// <param name="namespaceValue">The namespace being refreshed, for logging.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns><see langword="true"/> when the entry was replaced with a fresh response.</returns>
    private async Task<bool> TryRefreshKeyAsync(
        string key,
        string namespaceValue,
        CancellationToken cancellationToken)
    {
        var entry = await GetAsync<CachedResponse>(key, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        if (entry.VariesByRequest)
        {
            // Replaying another caller's request would mean impersonating them.
            ActionCacheLog.RefreshKeySkippedVaryBy(Logger, key, namespaceValue);
            return false;
        }

        if (entry.Request is null)
        {
            ActionCacheLog.RefreshKeySkipped(Logger, key, namespaceValue, "no request was recorded for it");
            return false;
        }

        var replayed = await RefreshProvider.ReplayAsync(entry.Request, cancellationToken);
        if (replayed is null)
        {
            return false;
        }

        // Written with the expirations the cached endpoint declared rather than this filter's.
        // A refresh filter is created without any, so using them would replace a declared
        // expiration with the global default -- one refresh made a bounded entry permanent.
        await SetAsync(key, replayed.Response, replayed.EntryOptions, cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public abstract Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task RemoveAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default) =>
        SetAsync(key, value, entryOptions: null, cancellationToken);

    /// <summary>
    /// Stores a value under the specified key, expiring it by the given options rather than
    /// the ones this cache was created with.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to store.</typeparam>
    /// <param name="key">The key of the value to store.</param>
    /// <param name="value">The value to store. Can be null.</param>
    /// <param name="entryOptions">
    /// The expirations to write this entry with, or <see langword="null"/> to use this cache's own.
    /// </param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous set operation.</returns>
    /// <remarks>
    /// <para>
    /// Deliberately not on <see cref="IActionCache"/>. Refresh is its only caller: a refresh
    /// filter is created without expirations, so writing a replayed response through this
    /// cache's own options would replace whatever the cached endpoint declared with the global
    /// defaults. A namespace can hold entries from several endpoints with different
    /// expirations, so the value has to be chosen per write rather than per cache — which is
    /// also why this is a parameter and not a constructor argument.
    /// </para>
    /// <para>
    /// It stays protected because the refresh loop calls it on <c>this</c>: a refresh never
    /// travels back out through <c>ActionCacheHandler</c> or <c>ResilientActionCache</c>, so
    /// neither needs to carry it, and no caller outside this hierarchy has any use for it.
    /// A caller wanting a cache with particular expirations asks the factory for one.
    /// </para>
    /// </remarks>
    protected abstract Task SetAsync<TValue>(string key, TValue? value, ActionCacheEntryOptions? entryOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// The options one write should use: the caller's expirations when given, this cache's own
    /// otherwise.
    /// </summary>
    /// <param name="entryOptions">The caller's expirations, or <see langword="null"/>.</param>
    /// <returns>The options to write the entry with.</returns>
    /// <remarks>
    /// Only the expirations are taken from the caller. <see cref="ActionCacheEntryOptions.LockTimeout"/>
    /// describes how long this cache waits on its key index, which is a property of the cache
    /// rather than of any entry written to it.
    /// </remarks>
    protected ActionCacheEntryOptions EffectiveEntryOptions(ActionCacheEntryOptions? entryOptions) =>
        entryOptions is null
            ? EntryOptions
            : new ActionCacheEntryOptions
            {
                AbsoluteExpiration = entryOptions.AbsoluteExpiration,
                SlidingExpiration = entryOptions.SlidingExpiration,
                LockTimeout = EntryOptions.LockTimeout
            };
}
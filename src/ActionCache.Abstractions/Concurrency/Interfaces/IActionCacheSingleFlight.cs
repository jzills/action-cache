using ActionCache.Utilities;

namespace ActionCache.Common.Concurrency;

/// <summary>
/// Coalesces concurrent cache misses for one key so that a hot entry's expiry does not
/// stampede the origin action.
/// </summary>
public interface IActionCacheSingleFlight
{
    /// <summary>
    /// Called after a miss has already been observed. Acquires the key's lock, re-reads the
    /// cache, and either returns what another request stored while this one waited or runs
    /// <paramref name="valueFactory"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the cached value.</typeparam>
    /// <param name="namespace">The cache namespace, which scopes the lock.</param>
    /// <param name="key">The cache key being contended.</param>
    /// <param name="cacheReader">Re-reads the cache once the lock is held.</param>
    /// <param name="valueFactory">Produces the value when this request is the leader.</param>
    /// <returns>The value and whether it came from another request.</returns>
    /// <remarks>
    /// Never throws on lock-acquisition failure: a timeout falls through to
    /// <paramref name="valueFactory"/> uncoalesced, consistent with the library's fail-open stance.
    /// </remarks>
    Task<SingleFlightResult<TValue>> GetOrCreateAsync<TValue>(
        Namespace @namespace,
        string key,
        Func<Task<TValue?>> cacheReader,
        Func<Task<TValue?>> valueFactory);
}

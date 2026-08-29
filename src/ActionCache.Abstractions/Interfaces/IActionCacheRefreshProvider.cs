using ActionCache.Common.Responses;

namespace ActionCache.Common.Caching;

/// <summary>
/// Re-issues the request that produced a cache entry so the entry can be refreshed with a
/// current response.
/// </summary>
public interface IActionCacheRefreshProvider
{
    /// <summary>
    /// Replays a recorded request and returns the response it produced.
    /// </summary>
    /// <param name="request">The request line recorded when the entry was cached.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>
    /// The response the replayed request produced and the expirations to rewrite the entry
    /// with, or <see langword="null"/> when it could not be replayed — no endpoint matched, or
    /// the response was not cacheable.
    /// </returns>
    Task<ActionCacheReplayResult?> ReplayAsync(CachedRequest request, CancellationToken cancellationToken = default);
}

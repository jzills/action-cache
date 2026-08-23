using ActionCache.Common.Caching;
using ActionCache.Common.Responses;

namespace Unit.TestUtilities;

/// <summary>
/// A refresh provider that replays nothing, for the cache fixtures that never refresh.
/// </summary>
internal sealed class NullRefreshProvider : IActionCacheRefreshProvider
{
    internal static readonly NullRefreshProvider Instance = new();

    private NullRefreshProvider()
    {
    }

    public Task<CachedResponse?> ReplayAsync(
        CachedRequest request,
        CancellationToken cancellationToken = default) => Task.FromResult<CachedResponse?>(null);
}

using Microsoft.AspNetCore.Http;

namespace ActionCache.Common.Caching;

/// <summary>
/// Marks an <see cref="HttpContext"/> as a refresh replay, so the cache filters execute
/// the endpoint instead of serving it from cache.
/// </summary>
/// <remarks>
/// Without this, a replay would run through the very <c>[ActionCache]</c> filter that
/// produced the entry, be served the stale value it is trying to replace, and write that
/// same stale value back — refresh would be a silent no-op. The replay reads through to the
/// action; the refresh loop, not the filter, stores the result.
/// </remarks>
internal static class ActionCacheReplayMarker
{
    private const string Key = "ActionCache.RefreshReplay";

    /// <summary>
    /// Marks the context as a refresh replay.
    /// </summary>
    /// <param name="httpContext">The synthetic context used for the replay.</param>
    internal static void Mark(HttpContext httpContext) => httpContext.Items[Key] = true;

    /// <summary>
    /// Whether the context is a refresh replay.
    /// </summary>
    /// <param name="httpContext">The current context.</param>
    /// <returns><see langword="true"/> when the request is a refresh replay.</returns>
    internal static bool IsReplay(HttpContext httpContext) => httpContext.Items.ContainsKey(Key);
}

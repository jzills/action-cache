using Microsoft.AspNetCore.Http;

namespace Unit.Common.Filters;

/// <summary>
/// Mirrors the library's internal replay marker so tests can simulate a refresh replay
/// without making the marker public.
/// </summary>
internal static class ActionCacheReplayMarkerAccessor
{
    internal static void Mark(HttpContext httpContext) =>
        httpContext.Items["ActionCache.RefreshReplay"] = true;
}

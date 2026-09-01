using ActionCache;
using ActionCache.Common;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Responses;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.TestUtilities.Builders;

internal static class SingleFlightBuilder
{
    internal static IActionCacheSingleFlight Build() =>
        Build(new ActionCacheSingleFlightOptions());

    internal static IActionCacheSingleFlight Build(ActionCacheSingleFlightOptions options) =>
        new InProcessSingleFlight(options, NullLogger<InProcessSingleFlight>.Instance);
}

internal static class VaryByBuilder
{
    internal static ActionCacheVaryByResolver Resolver() =>
        new([], NullLogger<ActionCacheVaryByResolver>.Instance);

    internal static VaryByOptions Options() => new();
}

internal static class ResponseFactoryBuilder
{
    internal static CachedResponseFactory Build() =>
        new(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}

internal static class ResilientCacheBuilder
{
    internal static IActionCache Decorate(IActionCache cache) =>
        new ActionCache.Common.Caching.ResilientActionCache(
            cache, NullLogger.Instance, failClosed: false);
}

using ActionCache.Common;
using ActionCache.Common.Concurrency;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.TestUtilities.Builders;

internal static class SingleFlightBuilder
{
    internal static IActionCacheSingleFlight Build() =>
        new InProcessSingleFlight(new ActionCacheEntryOptions(), NullLogger<InProcessSingleFlight>.Instance);
}

using ActionCache;
using ActionCache.Common;
using ActionCache.Common.Caching;
using Unit.TestUtilities;
using ActionCache.Common.Concurrency;
using ActionCache.Memory;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Unit.TestUtilities.Builders;

internal static class MemoryActionCacheFactoryBuilder
{
    internal static IActionCacheFactory Build()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        return new MemoryActionCacheFactory(
            memoryCache,
            new ExpirationTokenSourcesValidated(new ExpirationTokenSources(memoryCache)),
            Options.Create(new ActionCacheEntryOptions()),
            NullRefreshProvider.Instance,
            NullLoggerFactory.Instance,
            new SemaphoreSlimCacheLocker(TimeSpan.FromSeconds(10)));
    }
}

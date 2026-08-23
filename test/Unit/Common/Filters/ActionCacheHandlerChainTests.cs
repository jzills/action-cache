using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Enums;
using ActionCache.Common.Filters;
using ActionCache.Common.Keys;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Responses;
using FluentAssertions;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unit.TestUtilities.Builders;

namespace Unit.Common.Filters;

/// <summary>
/// Every layer of a chained backend configuration is reachable through the handler.
/// </summary>
/// <remarks>
/// <c>CreateHandler</c> called <c>SetNext</c> repeatedly on the head handler, and each call
/// assigned over the previous one. The chain was therefore first -> last for any number of
/// layers, and every layer in between was unreachable: never read, written, evicted or
/// refreshed, and missing from the <c>GetKeysAsync</c> union that eviction and refresh
/// drive off. Three or more instances is the shape that breaks — three backends, or a
/// comma-separated namespace with two.
/// </remarks>
[TestFixture]
public class ActionCacheHandlerChainTests
{
    /// <summary>
    /// Captures the handler <c>CreateHandler</c> builds, which is otherwise only ever
    /// handed to a concrete filter.
    /// </summary>
    private sealed class CapturingFactory : ActionCacheFilterAbstractFactoryBase<object>
    {
        public ActionCacheHandler? Captured { get; private set; }

        public CapturingFactory(TemplateBinderFactory binderFactory, ILoggerFactory loggerFactory)
            : base([],
                   binderFactory,
                   new ResilientCacheDecorator(
                       loggerFactory,
                       Options.Create(new ActionCacheResilienceOptions())),
                   loggerFactory,
                   new InProcessSingleFlight(
                       new ActionCacheSingleFlightOptions(),
                       NullLogger<InProcessSingleFlight>.Instance),
                   VaryByBuilder.Resolver(),
                   ResponseFactoryBuilder.Build(),
                   new ActionCacheKeyOptions())
        {
        }

        internal override object CreateFilter(
            ActionCacheHandler cache,
            FilterType type,
            bool singleFlight,
            VaryByOptions varyByOptions)
        {
            Captured = cache;
            return new object();
        }
    }

    private static CapturingFactory BuildFactory()
    {
        var services = new ServiceCollection().AddLogging().AddRouting().BuildServiceProvider();

        return new CapturingFactory(
            services.GetRequiredService<TemplateBinderFactory>(),
            services.GetRequiredService<ILoggerFactory>());
    }

    [Test]
    public async Task EveryLayerOfAThreeDeepChain_IsReachable()
    {
        var cacheFactory = MemoryActionCacheFactoryBuilder.Build();

        // Three backends holding one key each, the way three Use...Cache() calls produce
        // three instances for one namespace.
        var caches = new List<IActionCache>
        {
            cacheFactory.Create("ChainFirst")!,
            cacheFactory.Create("ChainMiddle")!,
            cacheFactory.Create("ChainLast")!
        };

        await caches[0].SetAsync("KeyFirst", "first");
        await caches[1].SetAsync("KeyMiddle", "middle");
        await caches[2].SetAsync("KeyLast", "last");

        var factory = BuildFactory();
        factory.CreateHandler(caches, FilterType.Add);

        var keys = await factory.Captured!.GetKeysAsync();

        keys.Should().BeEquivalentTo(["KeyFirst", "KeyMiddle", "KeyLast"],
            "eviction and refresh drive off the union of every layer's keys");
    }

    [Test]
    public async Task AWriteThroughTheChain_ReachesTheMiddleLayer()
    {
        var cacheFactory = MemoryActionCacheFactoryBuilder.Build();

        var caches = new List<IActionCache>
        {
            cacheFactory.Create("WriteFirst")!,
            cacheFactory.Create("WriteMiddle")!,
            cacheFactory.Create("WriteLast")!
        };

        var factory = BuildFactory();
        factory.CreateHandler(caches, FilterType.Add);

        await factory.Captured!.SetAsync("Key", "value");

        // The middle layer was silently skipped, so it never held what it was meant to
        // serve and an eviction never cleared it.
        (await caches[1].GetAsync<string>("Key")).Should().Be("value");

        await factory.Captured!.RemoveAsync();

        (await caches[1].GetAsync<string>("Key")).Should().BeNull();
    }
}

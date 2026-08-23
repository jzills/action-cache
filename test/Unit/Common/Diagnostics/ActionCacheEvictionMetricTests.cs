using System.Diagnostics.Metrics;
using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Common.Diagnostics;
using ActionCache.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Unit.TestUtilities.Builders;

namespace Unit.Common.Diagnostics;

/// <summary>
/// One eviction request, one measurement on <c>actioncache.evictions</c>.
/// </summary>
/// <remarks>
/// The counter used to live on the resilience decorator, which wraps each backend
/// individually. <c>ActionCacheHandler</c> fans a namespace eviction out to every layer, so
/// one <c>[ActionCacheEviction]</c> request against a Memory + Redis + SQL chain published
/// three evictions — the published figure counted backend calls, not evictions. This is the
/// same defect <c>actioncache.requests</c> was moved off that decorator to fix.
/// </remarks>
[TestFixture]
public class ActionCacheEvictionMetricTests
{
    private IActionCacheFactory _factory = null!;
    private TemplateBinderFactory _binderFactory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = MemoryActionCacheFactoryBuilder.Build();
        _binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();
    }

    private sealed class EvictionCollector : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<string?> Namespaces { get; } = [];

        public EvictionCollector()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ActionCacheDiagnostics.MeterName &&
                    instrument.Name == "actioncache.evictions")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "namespace")
                    {
                        Namespaces.Add(tag.Value?.ToString());
                    }
                }
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    /// <summary>
    /// Chains one decorated cache per simulated backend, the way the filter factory does.
    /// </summary>
    private ActionCacheHandler LayeredChain(string @namespace, int layers)
    {
        var handler = new ActionCacheHandler(
            ResilientCacheBuilder.Decorate(_factory.Create(@namespace)!));

        for (var layer = 1; layer < layers; layer++)
        {
            handler.SetNext(ResilientCacheBuilder.Decorate(_factory.Create(@namespace)!));
        }

        return handler;
    }

    private async Task EvictAsync(IActionCache cache)
    {
        var routeValues = new RouteValueDictionary
        {
            { "controller", "controller" },
            { "action", "action" }
        };

        var context = new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(routeValues), new ActionDescriptor()),
            [],
            new Dictionary<string, object?>(),
            controller: null!);

        var filter = new ActionCacheEvictionFilter(cache, _binderFactory, NullLogger.Instance);

        await filter.OnActionExecutionAsync(context, () =>
            Task.FromResult(new ActionExecutedContext(context, [], new object())));
    }

    [Test]
    public async Task AnEvictionOverALayeredChain_RecordsExactlyOneEviction()
    {
        using var collector = new EvictionCollector();

        await EvictAsync(LayeredChain("Layered", layers: 3));

        collector.Namespaces.Should().ContainSingle(
            "the request evicted one namespace, however many backends hold it");
    }

    [Test]
    public async Task AnEviction_TagsTheNamespaceTemplate()
    {
        using var collector = new EvictionCollector();

        await EvictAsync(LayeredChain("Account:{id}", layers: 1));

        collector.Namespaces.Should().ContainSingle().Which.Should().Be("Account:{id}");
    }
}

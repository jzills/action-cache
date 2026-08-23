using System.Diagnostics.Metrics;
using ActionCache;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Diagnostics;
using ActionCache.Common.Keys;
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
/// One request, one measurement on <c>actioncache.requests</c>.
/// </summary>
/// <remarks>
/// The counter used to live on the resilience decorator, which sees every backend read.
/// With single flight on by default a miss reads the backend twice — once before the lock
/// and once under it — so a single request published two misses and the hit ratio derived
/// from the counter was wrong for every application that had not turned single flight off.
/// </remarks>
[TestFixture]
public class ActionCacheFilterRequestMetricTests
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

    private sealed class RequestCollector : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<string?> Statuses { get; } = [];

        public RequestCollector()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ActionCacheDiagnostics.MeterName &&
                    instrument.Name == "actioncache.requests")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "status")
                    {
                        lock (Statuses)
                        {
                            Statuses.Add(tag.Value?.ToString());
                        }
                    }
                }
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    private ActionCacheFilter CreateFilter(IActionCache cache) =>
        new(cache, _binderFactory, NullLogger.Instance,
            new InProcessSingleFlight(new ActionCacheSingleFlightOptions(), NullLogger<InProcessSingleFlight>.Instance),
            singleFlightEnabled: true, VaryByBuilder.Resolver(), VaryByBuilder.Options(),
            ResponseFactoryBuilder.Build(), new ActionCacheKeyOptions());

    private static ActionExecutingContext BuildContext()
    {
        var routeValues = new RouteValueDictionary
        {
            { "controller", "controller" },
            { "action", "action" }
        };

        return new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(routeValues), new ActionDescriptor()),
            [],
            new Dictionary<string, object?>(),
            controller: null!);
    }

    private async Task InvokeAsync(IActionCache cache)
    {
        var context = BuildContext();
        await CreateFilter(cache).OnActionExecutionAsync(context, () =>
            Task.FromResult(new ActionExecutedContext(context, [], new object())
            {
                Result = new OkObjectResult("fresh")
            }));
    }

    [Test]
    public async Task OneMissingRequest_RecordsExactlyOneMiss()
    {
        using var collector = new RequestCollector();
        var cache = ResilientCacheBuilder.Decorate(
            _factory.Create(nameof(OneMissingRequest_RecordsExactlyOneMiss))!);

        await InvokeAsync(cache);

        collector.Statuses.Should().Equal("miss");
    }

    [Test]
    public async Task ASecondRequestForTheSameKey_RecordsExactlyOneHit()
    {
        var cache = ResilientCacheBuilder.Decorate(
            _factory.Create(nameof(ASecondRequestForTheSameKey_RecordsExactlyOneHit))!);

        await InvokeAsync(cache);

        using var collector = new RequestCollector();
        await InvokeAsync(cache);

        collector.Statuses.Should().Equal("hit");
    }

    [Test]
    public async Task ThirtyConcurrentMisses_RecordOneMeasurementEach()
    {
        using var collector = new RequestCollector();
        var cache = ResilientCacheBuilder.Decorate(
            _factory.Create(nameof(ThirtyConcurrentMisses_RecordOneMeasurementEach))!);

        await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(() => InvokeAsync(cache))));

        collector.Statuses.Should().HaveCount(30, "each request contributes exactly one measurement");
    }
}

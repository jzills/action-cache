using ActionCache;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Enums;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Unit.Common.Filters;

[TestFixture]
public class ActionCacheFilterSingleFlightTests
{
    private TemplateBinderFactory _binderFactory = null!;

    [SetUp]
    public void SetUp() =>
        _binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();

    /// <summary>
    /// A minimal in-memory cache standing in for a backend, so concurrency is exercised
    /// against real state rather than a mock's call sequence.
    /// </summary>
    private sealed class StubCache : IActionCache
    {
        private readonly Dictionary<string, object?> _entries = [];
        private readonly object _gate = new();

        public Namespace GetNamespace() => new("Test");

        public Task<TValue?> GetAsync<TValue>(string key)
        {
            lock (_gate)
            {
                return Task.FromResult(_entries.TryGetValue(key, out var value) ? (TValue?)value : default);
            }
        }

        public Task SetAsync<TValue>(string key, TValue? value)
        {
            lock (_gate)
            {
                _entries[key] = value;
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetKeysAsync()
        {
            lock (_gate)
            {
                return Task.FromResult(_entries.Keys.AsEnumerable());
            }
        }

        public Task RemoveAsync(string key) => Task.CompletedTask;
        public Task RemoveAsync() => Task.CompletedTask;
        public Task RefreshAsync() => Task.CompletedTask;
    }

    private ActionCacheFilter CreateFilter(IActionCache cache, IActionCacheSingleFlight singleFlight, bool enabled) =>
        new(cache, _binderFactory, NullLogger.Instance, singleFlight, enabled);

    private static InProcessSingleFlight CreateSingleFlight() =>
        new(new ActionCache.Common.ActionCacheEntryOptions(), NullLogger<InProcessSingleFlight>.Instance);

    [Test]
    public async Task OnActionExecutionAsync_UnderConcurrentMisses_ExecutesTheActionOnce()
    {
        var cache = new StubCache();
        var singleFlight = CreateSingleFlight();
        var actionExecutions = 0;

        async Task Invoke()
        {
            var context = BuildActionExecutingContext();
            var filter = CreateFilter(cache, singleFlight, enabled: true);

            await filter.OnActionExecutionAsync(context, async () =>
            {
                Interlocked.Increment(ref actionExecutions);
                await Task.Delay(25);
                return new ActionExecutedContext(context, [], new object())
                {
                    Result = new OkObjectResult("fresh")
                };
            });
        }

        await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(Invoke)));

        actionExecutions.Should().Be(1);
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCoalesced_ReportsAHitAndSetsTheCachedResult()
    {
        var cache = new StubCache();
        var singleFlight = CreateSingleFlight();
        var leaderResult = new OkObjectResult("fresh");

        // Leader populates the cache.
        var leaderContext = BuildActionExecutingContext();
        await CreateFilter(cache, singleFlight, enabled: true).OnActionExecutionAsync(
            leaderContext,
            () => Task.FromResult(new ActionExecutedContext(leaderContext, [], new object()) { Result = leaderResult }));

        // Waiter observes the leader's entry on its first read — the Hit path.
        var waiterContext = BuildActionExecutingContext();
        var waiterExecuted = false;
        await CreateFilter(cache, singleFlight, enabled: true).OnActionExecutionAsync(waiterContext, () =>
        {
            waiterExecuted = true;
            return Task.FromResult(new ActionExecutedContext(waiterContext, [], new object()));
        });

        waiterExecuted.Should().BeFalse();
        waiterContext.Result.Should().Be(leaderResult);
        waiterContext.HttpContext.Response.Headers[CacheHeaders.CacheStatus]
            .ToString().Should().Be(nameof(CacheStatus.Hit));
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenSingleFlightIsDisabled_BypassesTheService()
    {
        var cache = new StubCache();
        var singleFlightMock = new Mock<IActionCacheSingleFlight>(MockBehavior.Strict);
        var context = BuildActionExecutingContext();
        var executed = false;

        await CreateFilter(cache, singleFlightMock.Object, enabled: false)
            .OnActionExecutionAsync(context, () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(context, [], new object())
                {
                    Result = new OkObjectResult("fresh")
                });
            });

        executed.Should().BeTrue();
        singleFlightMock.VerifyNoOtherCalls();
        context.HttpContext.Response.Headers[CacheHeaders.CacheStatus]
            .ToString().Should().Be(nameof(CacheStatus.Add));
    }

    private static ActionExecutingContext BuildActionExecutingContext()
    {
        var routeValues = new RouteValueDictionary
        {
            { "area", "area" },
            { "controller", "controller" },
            { "action", "action" }
        };

        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData(routeValues);
        var actionDescriptor = new ActionDescriptor();
        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);

        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    }
}

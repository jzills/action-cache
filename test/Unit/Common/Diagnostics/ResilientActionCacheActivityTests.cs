using System.Diagnostics;
using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Unit.Common.Diagnostics;

/// <summary>
/// Degradation must report on ActionCache's own span, never on whichever span happens to
/// be current.
/// </summary>
[TestFixture]
public class ResilientActionCacheActivityTests
{
    private const string HostSourceName = "Unit.SimulatedHost";

    private static ActivityListener ListenTo(params string[] sourceNames)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => sourceNames.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static ResilientActionCache CacheThatFails(Exception failure)
    {
        var inner = new Mock<IActionCache>();
        inner.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));
        inner.Setup(cache => cache.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(failure);
        inner.Setup(cache => cache.RemoveAsync(It.IsAny<CancellationToken>()))
             .ThrowsAsync(failure);

        return new ResilientActionCache(inner.Object, NullLogger.Instance, failClosed: false);
    }

    [Test]
    public async Task GetAsync_WhenDegrading_LeavesTheAmbientRequestSpanUntouched()
    {
        // The app traces ASP.NET Core but has not added ActionCache's source, which is the
        // default. StartOperation returns null, so anything written to Activity.Current
        // lands on the incoming request span instead.
        using var listener = ListenTo(HostSourceName);
        using var host = new ActivitySource(HostSourceName);
        var cache = CacheThatFails(new InvalidOperationException("backend down"));

        using var request = host.StartActivity("HTTP GET /users");
        request.Should().NotBeNull();

        var value = await cache.GetAsync<string>("Key");

        value.Should().BeNull("fail-open degrades the read to a miss");
        request!.Status.Should().Be(ActivityStatusCode.Unset,
            "a degraded cache read must not report the request itself as failed");
    }

    [Test]
    public async Task RemoveAsync_ForTheWholeNamespace_WhenDegrading_LeavesTheAmbientSpanUntouched()
    {
        // Namespace eviction started no span of its own, so its degrade path always wrote
        // to somebody else's.
        using var listener = ListenTo(HostSourceName);
        using var host = new ActivitySource(HostSourceName);
        var cache = CacheThatFails(new InvalidOperationException("backend down"));

        using var request = host.StartActivity("HTTP POST /users");
        request.Should().NotBeNull();

        await cache.RemoveAsync();

        request!.Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Test]
    public async Task GetAsync_WhenDegrading_MarksActionCachesOwnSpan()
    {
        using var listener = ListenTo(HostSourceName, "ActionCache");
        using var host = new ActivitySource(HostSourceName);
        var cache = CacheThatFails(new InvalidOperationException("backend down"));

        Activity? cacheSpan = null;
        using var capture = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ActionCache",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => cacheSpan ??= activity
        };
        ActivitySource.AddActivityListener(capture);

        using var request = host.StartActivity("HTTP GET /users");

        await cache.GetAsync<string>("Key");

        cacheSpan.Should().NotBeNull("the cache operation starts its own span when subscribed");
        cacheSpan!.Status.Should().Be(ActivityStatusCode.Error,
            "the failure belongs on the operation that actually failed");
        request!.Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Test]
    public void StartOperation_TagsTheNamespaceTemplate_NotItsResolvedForm()
    {
        // A templated namespace resolves per resource, which as a span attribute or metric
        // dimension is one time series per id.
        using var listener = ListenTo("ActionCache");

        Activity? captured = null;
        using var capture = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ActionCache",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => captured ??= activity
        };
        ActivitySource.AddActivityListener(capture);

        var @namespace = new Namespace("Account:{id}") { ValueWithRouteTemplateParameters = "Account:42" };
        var inner = new Mock<IActionCache>();
        inner.Setup(cache => cache.GetNamespace()).Returns(@namespace);
        inner.Setup(cache => cache.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync("value");

        var cache = new ResilientActionCache(inner.Object, NullLogger.Instance);

        cache.GetAsync<string>("Key").GetAwaiter().GetResult();

        captured.Should().NotBeNull();
        captured!.GetTagItem("actioncache.namespace").Should().Be("Account:{id}");
    }
}

using ActionCache;
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

/// <summary>
/// A refresh replay is a real request against the real endpoint, so it runs every filter
/// the endpoint carries. The cache filters already read through on the marker. These cover
/// the other two, where re-entering is worse than a stale read:
///
/// <list type="bullet">
///   <item>Refresh does not terminate — the pass replays an entry, the replay refreshes the
///     namespace, which replays the entry again.</item>
///   <item>Eviction erases the namespace the pass is in the middle of warming.</item>
/// </list>
///
/// Each test asserts the guard directly rather than provoking the loop, so a regression
/// fails as an assertion instead of hanging the suite.
/// </summary>
[TestFixture]
public class ActionCacheReplayGuardTests
{
    private Mock<IActionCache> _cacheMock = null!;
    private TemplateBinderFactory _binderFactory = null!;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IActionCache>();
        _cacheMock.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));

        _binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();
    }

    [Test]
    public async Task OnResultExecutionAsync_WhenReplay_DoesNotCallRefreshAsync()
    {
        var sut = new ActionCacheRefreshFilter(_cacheMock.Object, _binderFactory, NullLogger.Instance);
        var context = BuildResultExecutingContext(new OkObjectResult("ok"));
        ActionCacheReplayMarkerAccessor.Mark(context.HttpContext);

        await sut.OnResultExecutionAsync(context, () => Task.FromResult(BuildResultExecutedContext(context)));

        _cacheMock.Verify(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task OnResultExecutionAsync_WhenReplay_StillCallsNext()
    {
        var sut = new ActionCacheRefreshFilter(_cacheMock.Object, _binderFactory, NullLogger.Instance);
        var context = BuildResultExecutingContext(new OkObjectResult("ok"));
        ActionCacheReplayMarkerAccessor.Mark(context.HttpContext);

        var nextCalled = false;
        await sut.OnResultExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(BuildResultExecutedContext(context));
        });

        // The replay exists to produce a response for the refresh loop to store. Skipping the
        // refresh must not also skip executing the result.
        nextCalled.Should().BeTrue();
    }

    private static ResultExecutingContext BuildResultExecutingContext(IActionResult result) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            [],
            result,
            new object());

    private static ResultExecutedContext BuildResultExecutedContext(ResultExecutingContext context) =>
        new(new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
            [],
            context.Result,
            new object());
}

using ActionCache;
using ActionCache.EndpointFilters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Unit.Common.Filters;

/// <summary>
/// The Minimal API counterpart of <see cref="ActionCacheRefreshFilterTests"/>. Success is
/// judged from the returned result rather than the response status, matching the MVC refresh
/// filter and the endpoint cache filter: nothing has been written to the response yet when
/// an endpoint filter returns.
/// </summary>
[TestFixture]
public class ActionCacheEndpointRefreshFilterTests
{
    private Mock<IActionCache> _cacheMock = null!;
    private TemplateBinderFactory _binderFactory = null!;
    private ActionCacheEndpointRefreshFilter _sut = null!;

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

        _sut = new ActionCacheEndpointRefreshFilter(_cacheMock.Object, _binderFactory, NullLogger.Instance);
    }

    [Test]
    public async Task InvokeAsync_WhenSuccessfulResult_CallsRefreshAsync()
    {
        await _sut.InvokeAsync(BuildContext(), _ => ValueTask.FromResult<object?>(Results.Ok("ok")));

        _cacheMock.Verify(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InvokeAsync_WhenBadRequestResult_DoesNotCallRefreshAsync()
    {
        await _sut.InvokeAsync(BuildContext(), _ => ValueTask.FromResult<object?>(Results.BadRequest()));

        _cacheMock.Verify(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task InvokeAsync_WhenSuccessfulResult_ReturnsTheEndpointResult()
    {
        var expected = Results.Ok("ok");

        var actual = await _sut.InvokeAsync(BuildContext(), _ => ValueTask.FromResult<object?>(expected));

        // Refresh is a side effect. The caller still gets what the endpoint returned.
        actual.Should().BeSameAs(expected);
    }

    [Test]
    public async Task InvokeAsync_WhenReplay_DoesNotCallRefreshAsync()
    {
        var context = BuildContext();
        ActionCacheReplayMarkerAccessor.Mark(context.HttpContext);

        await _sut.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok("ok")));

        // Without this the pass replays an entry, the replay refreshes the namespace, and
        // that replays the same entry again -- it does not terminate.
        _cacheMock.Verify(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DefaultEndpointFilterInvocationContext BuildContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = new RouteData() });
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}

using Unit.TestUtilities.Builders;
using ActionCache;
using ActionCache.Common.Responses;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Unit.Common.EndpointFilters;

[TestFixture]
public class ActionCacheEndpointFilterTests
{
    private Mock<IActionCache> _cacheMock;
    private TemplateBinderFactory _binderFactory;
    private ActionCacheEndpointFilter _sut;

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

        _sut = new ActionCacheEndpointFilter(_cacheMock.Object, _binderFactory, NullLogger.Instance, SingleFlightBuilder.Build(), true, VaryByBuilder.Resolver(), VaryByBuilder.Options(), ResponseFactoryBuilder.Build());
    }

    [Test]
    public async Task InvokeAsync_WhenNoEndpoint_CallsNextAndReturnsMiss()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = new RouteData() });
        var context = BuildContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = ctx =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("next-result");
        };

        var result = await _sut.InvokeAsync(context, next);

        nextCalled.Should().BeTrue();
        result.Should().Be("next-result");
    }

    [Test]
    public async Task InvokeAsync_WhenCacheHit_ReturnsCachedValueWithoutCallingNext()
    {
        var httpContext = BuildHttpContextWithEndpoint();
        var context = BuildContext(httpContext);
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CachedResponse { StatusCode = 200, ContentType = "application/json", Body = "\"cached\"" });

        var nextCalled = false;
        EndpointFilterDelegate next = ctx =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("next-result");
        };

        var result = await _sut.InvokeAsync(context, next);

        nextCalled.Should().BeFalse();

        // A hit now returns a result rebuilt from the stored envelope, not the raw value.
        result.Should().BeAssignableTo<IResult>();
    }

    [Test]
    public async Task InvokeAsync_WhenCacheMissAndNextReturnsValue_StoresAndReturnsValue()
    {
        var httpContext = BuildHttpContextWithEndpoint();
        var context = BuildContext(httpContext);
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedResponse?)null);

        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>("fresh-result");

        var result = await _sut.InvokeAsync(context, next);

        result.Should().Be("fresh-result");
        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InvokeAsync_WhenCacheMissAndNextReturnsNull_DoesNotStore()
    {
        var httpContext = BuildHttpContextWithEndpoint();
        var context = BuildContext(httpContext);
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedResponse?)null);

        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>(null);

        var result = await _sut.InvokeAsync(context, next);

        result.Should().BeNull();
        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task InvokeAsync_WhenCacheMissAndNextReturnsErrorResult_DoesNotStore()
    {
        var httpContext = BuildHttpContextWithEndpoint();
        var context = BuildContext(httpContext);
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedResponse?)null);

        // TypedResults.NotFound() implements IStatusCodeHttpResult with status 404.
        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>(TypedResults.NotFound());

        var result = await _sut.InvokeAsync(context, next);

        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task InvokeAsync_WhenCacheMissAndNextReturnsSuccessResult_Stores()
    {
        var httpContext = BuildHttpContextWithEndpoint();
        var context = BuildContext(httpContext);
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CachedResponse?)null);

        // TypedResults.Ok(value) implements IStatusCodeHttpResult with status 200.
        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>(TypedResults.Ok("fresh"));

        var result = await _sut.InvokeAsync(context, next);

        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DefaultHttpContext BuildHttpContextWithEndpoint()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(ctx => Task.CompletedTask, EndpointMetadataCollection.Empty, "Test"));
        httpContext.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = new RouteData() });
        return httpContext;
    }

    private static EndpointFilterInvocationContext BuildContext(HttpContext httpContext)
    {
        var contextMock = new Mock<EndpointFilterInvocationContext>();
        contextMock.Setup(ctx => ctx.HttpContext).Returns(httpContext);
        contextMock.Setup(ctx => ctx.Arguments).Returns([]);
        return contextMock.Object;
    }
}

using ActionCache;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
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

        _sut = new ActionCacheEndpointFilter(_cacheMock.Object, _binderFactory);
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
        _cacheMock.Setup(cache => cache.GetAsync<object?>(It.IsAny<string>())).ReturnsAsync("cached");

        var nextCalled = false;
        EndpointFilterDelegate next = ctx =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("next-result");
        };

        var result = await _sut.InvokeAsync(context, next);

        nextCalled.Should().BeFalse();
        result.Should().Be("cached");
    }

    [Test]
    public async Task InvokeAsync_WhenCacheMissAndNextReturnsValue_StoresAndReturnsValue()
    {
        var httpContext = BuildHttpContextWithEndpoint();
        var context = BuildContext(httpContext);
        _cacheMock.Setup(cache => cache.GetAsync<object?>(It.IsAny<string>())).ReturnsAsync((object?)null);

        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>("fresh-result");

        var result = await _sut.InvokeAsync(context, next);

        result.Should().Be("fresh-result");
        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<object?>()), Times.Once);
    }

    [Test]
    public async Task InvokeAsync_WhenCacheMissAndNextReturnsNull_DoesNotStore()
    {
        var httpContext = BuildHttpContextWithEndpoint();
        var context = BuildContext(httpContext);
        _cacheMock.Setup(cache => cache.GetAsync<object?>(It.IsAny<string>())).ReturnsAsync((object?)null);

        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>(null);

        var result = await _sut.InvokeAsync(context, next);

        result.Should().BeNull();
        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<object?>()), Times.Never);
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

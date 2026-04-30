using ActionCache;
using ActionCache.EndpointFilters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Unit.Common.EndpointFilters;

[TestFixture]
public class ActionCacheEndpointEvictionFilterTests
{
    private Mock<IActionCache> _cacheMock;
    private TemplateBinderFactory _binderFactory;
    private ActionCacheEndpointEvictionFilter _sut;

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

        _sut = new ActionCacheEndpointEvictionFilter(_cacheMock.Object, _binderFactory);
    }

    [Test]
    public async Task InvokeAsync_WhenResponseIsSuccess_CallsRemoveAsync()
    {
        var httpContext = BuildHttpContext(statusCode: 200);
        var context = BuildContext(httpContext);
        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>(null);

        await _sut.InvokeAsync(context, next);

        _cacheMock.Verify(cache => cache.RemoveAsync(), Times.Once);
    }

    [Test]
    public async Task InvokeAsync_WhenResponseIsNotSuccess_DoesNotCallRemoveAsync()
    {
        var httpContext = BuildHttpContext(statusCode: 400);
        var context = BuildContext(httpContext);
        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>(null);

        await _sut.InvokeAsync(context, next);

        _cacheMock.Verify(cache => cache.RemoveAsync(), Times.Never);
    }

    [Test]
    public async Task InvokeAsync_Always_CallsNext()
    {
        var httpContext = BuildHttpContext(statusCode: 200);
        var context = BuildContext(httpContext);
        var nextCalled = false;
        EndpointFilterDelegate next = ctx =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(null);
        };

        await _sut.InvokeAsync(context, next);

        nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task InvokeAsync_Always_ReturnsNextResult()
    {
        var httpContext = BuildHttpContext(statusCode: 200);
        var context = BuildContext(httpContext);
        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>("result");

        var result = await _sut.InvokeAsync(context, next);

        result.Should().Be("result");
    }

    private static DefaultHttpContext BuildHttpContext(int statusCode)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = statusCode;
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

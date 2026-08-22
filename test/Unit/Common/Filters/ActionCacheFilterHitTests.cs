using ActionCache.Common.Keys;
using Unit.TestUtilities.Builders;
using ActionCache;
using ActionCache.Common.Responses;
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
public class ActionCacheFilterHitTests
{
    private Mock<IActionCache> _cacheMock;
    private TemplateBinderFactory _binderFactory;
    private ActionCacheFilter _sut;

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

        _sut = new ActionCacheFilter(_cacheMock.Object, _binderFactory, NullLogger.Instance, SingleFlightBuilder.Build(), true, VaryByBuilder.Resolver(), VaryByBuilder.Options(), ResponseFactoryBuilder.Build(), new ActionCacheKeyOptions());
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCacheHit_ShortCircuitsWithCachedResult()
    {
        var cachedResponse = new CachedResponse { StatusCode = 200, ContentType = "application/json", Body = "\"cached\"" };
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var context = BuildActionExecutingContext();
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        };

        await _sut.OnActionExecutionAsync(context, next);

        nextCalled.Should().BeFalse();
        // The filter now rebuilds a ContentResult from the stored envelope rather than
        // handing back a deserialized result graph.
        var result = context.Result as ContentResult;
        result!.StatusCode.Should().Be(200);
        result.Content.Should().Be("\"cached\"");
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCacheMissAndResultIsNull_DoesNotSetCache()
    {
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedResponse?)null);

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
        {
            var executed = new ActionExecutedContext(context, [], new object())
            {
                Result = null
            };
            return Task.FromResult(executed);
        };

        await _sut.OnActionExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<CachedResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenEmptyRouteValues_CallsNextWithMissStatus()
    {
        var context = BuildActionExecutingContext(routeValues: new RouteValueDictionary());
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        };

        await _sut.OnActionExecutionAsync(context, next);

        nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCacheMissAndResultIsNonNull_StoresCacheEntry()
    {
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedResponse?)null);

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
        {
            var executed = new ActionExecutedContext(context, [], new object())
            {
                Result = new OkObjectResult("fresh")
            };
            return Task.FromResult(executed);
        };

        await _sut.OnActionExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<CachedResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCacheMissAndResultIsError_DoesNotStoreCacheEntry()
    {
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedResponse?)null);

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
        {
            var executed = new ActionExecutedContext(context, [], new object())
            {
                Result = new NotFoundObjectResult("missing")
            };
            return Task.FromResult(executed);
        };

        await _sut.OnActionExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<CachedResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCacheMissAndResultIsPocoObjectResult_StoresCacheEntry()
    {
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedResponse?)null);

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
        {
            // Null StatusCode == framework serializes as 200 (a POCO return).
            var executed = new ActionExecutedContext(context, [], new object())
            {
                Result = new ObjectResult("poco") { StatusCode = null }
            };
            return Task.FromResult(executed);
        };

        await _sut.OnActionExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<CachedResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCacheMissAndResultIsSuccessfulJsonResult_StoresCacheEntry()
    {
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedResponse?)null);

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
        {
            // JsonResult with a null StatusCode is serialized as 200 but is not
            // an ObjectResult; it must still be cached.
            var executed = new ActionExecutedContext(context, [], new object())
            {
                Result = new JsonResult(new { value = "x" })
            };
            return Task.FromResult(executed);
        };

        await _sut.OnActionExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<CachedResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCacheMissAndResultIsErrorJsonResult_DoesNotStoreCacheEntry()
    {
        _cacheMock.Setup(cache => cache.GetAsync<CachedResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedResponse?)null);

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
        {
            var executed = new ActionExecutedContext(context, [], new object())
            {
                Result = new JsonResult(new { error = "nope" }) { StatusCode = 404 }
            };
            return Task.FromResult(executed);
        };

        await _sut.OnActionExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<CachedResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ActionExecutingContext BuildActionExecutingContext(
        RouteValueDictionary? routeValues = null)
    {
        routeValues ??= new RouteValueDictionary
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

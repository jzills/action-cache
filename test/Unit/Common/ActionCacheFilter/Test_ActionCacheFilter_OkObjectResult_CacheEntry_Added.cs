using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Abstractions;
using ActionCache.Attributes;
using ActionCache.Filters;
using ActionCache;
using ActionCache.Common.Keys;
using Microsoft.AspNetCore.Routing.Template;
using Unit.TestUtilities.Builders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Common;

[TestFixture]
public class ActionCacheFilterTests
{
    private IActionCache _cache;
    private IActionCacheFactory _factory;
    private TemplateBinderFactory _binderFactory;

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

    [TearDown]
    public async Task TearDown() => await _cache.RemoveAsync();

    [Test]
    public async Task OnActionExecutionAsync_WhenOkObjectResult_AddsCacheEntry()
    {
        var @namespace = "Test";
        var routeValues = new RouteValueDictionary
        {
            { "area", "someArea" },
            { "controller", "someController" },
            { "action", "someAction" }
        };
        var routeData = new RouteData(routeValues);
        var metadata = new List<IFilterMetadata> { new ActionCacheAttribute { Namespace = @namespace } };
        var actionContext = new ActionContext(
            httpContext: new DefaultHttpContext(),
            routeData: routeData,
            actionDescriptor: new ActionDescriptor()
        );
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            metadata,
            new Dictionary<string, object>(),
            null);
        ActionExecutionDelegate next = () =>
        {
            var context = new ActionExecutedContext(actionExecutingContext, metadata, null)
            {
                Result = new OkObjectResult("Foo")
            };
            return Task.FromResult(context);
        };

        _cache = _factory.Create(@namespace)!;
        var filter = new ActionCacheFilter(_cache, _binderFactory, NullLogger.Instance, SingleFlightBuilder.Build(), true, VaryByBuilder.Resolver(), VaryByBuilder.Options());

        await filter.OnActionExecutionAsync(actionExecutingContext, next);

        var key = new ActionCacheKeyBuilder()
            .WithRouteValues(routeData.Values)
            .Build();
        var cacheResult = await _cache.GetAsync<IActionResult>(key);

        cacheResult.Should().BeAssignableTo<OkObjectResult>();
        cacheResult.As<OkObjectResult>().Value.Should().Be("Foo");
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Abstractions;
using ActionCache.Attributes;
using ActionCache.Filters;
using ActionCache;
using Microsoft.AspNetCore.Routing.Template;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheEvictionFilterTests
{
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

    [Test]
    public async Task OnActionExecutionAsync_Always_RemovesCacheEntry()
    {
        var @namespace = "Test";
        var routeValues = new RouteValueDictionary
        {
            { "area", "someArea" },
            { "controller", "someController" },
            { "action", "someAction" }
        };
        var metadata = new List<IFilterMetadata> { new ActionCacheAttribute { Namespace = @namespace } };
        var actionContext = new ActionContext(
            httpContext: new DefaultHttpContext(),
            routeData: new RouteData(routeValues),
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

        var cache = _factory.Create(@namespace)!;

        await cache.SetAsync("someArea:someController:someAction", "Foo");

        var filter = new ActionCacheEvictionFilter(cache, _binderFactory);
        await filter.OnActionExecutionAsync(actionExecutingContext, next);

        var result = await cache.GetAsync<string>("someArea:someController:someAction");

        result.Should().BeNull();
    }
}

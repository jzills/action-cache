using ActionCache.Common.Keys;
using Microsoft.AspNetCore.Routing;

namespace Unit.Common;

[TestFixture]
public class ActionCacheKeyComponentsBuilderTests
{
    [Test]
    public void Build_Always_ParsesRouteValuesAndActionArguments()
    {
        var actionArguments = new Dictionary<string, object>
        {
            { "Foo", "Bar" },
            { "Biz", 22222 }
        };
        var routeValues = new RouteValueDictionary
        {
            { "area", "someArea" },
            { "controller", "someController" },
            { "action", "someAction" }
        };
        var key = new ActionCacheKeyBuilder()
            .WithActionArguments(actionArguments)
            .WithRouteValues(routeValues)
            .Build();

        var keyComponents = new ActionCacheKeyComponentsBuilder(key).Build();

        keyComponents.ActionArguments.Should().ContainKey("Foo");
        keyComponents.ActionArguments!["Foo"].As<string>().Should().Be("Bar");

        keyComponents.ActionArguments.Should().ContainKey("Biz");
        keyComponents.ActionArguments["Biz"].As<long?>().Should().Be(22222);

        keyComponents.RouteValues.Should().ContainKey("area");
        keyComponents.RouteValues!["area"].As<string>().Should().Be("someArea");

        keyComponents.RouteValues.Should().ContainKey("controller");
        keyComponents.RouteValues["controller"].As<string>().Should().Be("someController");

        keyComponents.RouteValues.Should().ContainKey("action");
        keyComponents.RouteValues["action"].As<string>().Should().Be("someAction");
    }
}

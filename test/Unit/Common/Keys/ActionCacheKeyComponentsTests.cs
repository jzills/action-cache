using ActionCache.Common.Keys;
using Microsoft.AspNetCore.Routing;

namespace Unit.Common.Keys;

[TestFixture]
public class ActionCacheKeyComponentsTests
{
    [Test]
    public void Serialize_Always_ProducesQueryStringWithBothSections()
    {
        var components = new ActionCacheKeyComponents
        {
            RouteValues = new RouteValueDictionary { { "controller", "home" }, { "action", "index" } },
            ActionArguments = new Dictionary<string, object?> { { "id", 1 } }
        };

        var result = components.Serialize();

        result.Should().StartWith(ActionCacheKeyComponents.RouteValuesKey + "=");
        result.Should().Contain("&" + ActionCacheKeyComponents.ActionArgumentsKey + "=");
    }

    [Test]
    public void Serialize_WhenEmpty_ProducesEmptyJsonSections()
    {
        var components = new ActionCacheKeyComponents();

        var result = components.Serialize();

        result.Should().Contain(ActionCacheKeyComponents.RouteValuesKey + "=");
        result.Should().Contain(ActionCacheKeyComponents.ActionArgumentsKey + "=");
    }

    [Test]
    public void Deconstruct_WhenRouteValuesContainAllKeys_ReturnsAreaControllerAction()
    {
        var components = new ActionCacheKeyComponents
        {
            RouteValues = new RouteValueDictionary
            {
                { "area", "admin" },
                { "controller", "users" },
                { "action", "index" }
            }
        };

        var (area, controller, action) = components;

        area.Should().Be("admin");
        controller.Should().Be("users");
        action.Should().Be("index");
    }

    [Test]
    public void Deconstruct_WhenAreaMissing_ReturnsNullForArea()
    {
        var components = new ActionCacheKeyComponents
        {
            RouteValues = new RouteValueDictionary
            {
                { "controller", "home" },
                { "action", "index" }
            }
        };

        var (area, controller, action) = components;

        area.Should().BeNull();
        controller.Should().Be("home");
        action.Should().Be("index");
    }

    [Test]
    public void Deconstruct_WhenRouteValuesNull_ThrowsArgumentNullException()
    {
        var components = new ActionCacheKeyComponents { RouteValues = null };

        Action act = () =>
        {
            var (_, _, _) = components;
        };

        act.Should().Throw<ArgumentNullException>();
    }
}

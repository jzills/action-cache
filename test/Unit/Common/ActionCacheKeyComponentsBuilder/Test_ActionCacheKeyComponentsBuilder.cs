using ActionCache.Common.Keys;
using Microsoft.AspNetCore.Routing;

namespace Unit.Common;

[TestFixture]
public class ActionCacheKeyComponentsBuilderTests
{
    // Bug M1: ActionCacheKeyComponentsBuilder calls KeyEncoder.Decode(value) in its constructor
    // with no try-catch. A corrupted or manually crafted cache key that is not valid hex throws
    // FormatException with no context about which key was malformed.
    //
    // Fix: wrap the decode in try-catch, log the offending key, and either return a safe default
    // or rethrow with additional context.

    [Test]
    public void Constructor_WhenValueIsNotValidHex_ThrowsFormatException_BugM1()
    {
        const string malformedKey = "not-valid-hex!!";

        // BUG: throws FormatException with no context about the bad key.
        // Fix: catch and rethrow with key info, or return empty/default components.
        Action act = () => new ActionCacheKeyComponentsBuilder(malformedKey);

        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Constructor_WhenValueIsValidHexButNotQueryString_DoesNotThrow_BugM1()
    {
        // Valid hex that decodes to non-query-string content — should not throw
        // (the query parser treats unrecognised content as a single key with no value).
        var validHexOfArbitraryContent = new ActionCache.Common.Keys.KeyEncoder().Encode("not-a-query-string");

        Action act = () => new ActionCacheKeyComponentsBuilder(validHexOfArbitraryContent);

        act.Should().NotThrow();
    }

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

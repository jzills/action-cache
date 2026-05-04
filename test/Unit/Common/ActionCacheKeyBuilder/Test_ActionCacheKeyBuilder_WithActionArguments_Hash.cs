using Unit.TestUtiltiies.Data;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using ActionCache.Common.Keys;
using ActionCache.Common.Serialization;

namespace Unit.Common;

[TestFixture]
public class ActionCacheKeyBuilderTests
{
    // Bug H2: WithActionArguments calls actionArguments.ToDictionary() without a null guard.
    // Passing null throws NullReferenceException. The correct behavior is either an early
    // return (treat null as empty arguments) or a clear ArgumentNullException.

    [Test]
    public void WithActionArguments_WhenNull_ShouldNotThrow_BugH2()
    {
        var builder = new ActionCacheKeyBuilder();

        // BUG: currently throws NullReferenceException
        // Fix: add null guard and return early (treat as no arguments)
        Action act = () => builder.WithActionArguments(null!);

        act.Should().NotThrow();
    }

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetControllerDescriptors))]
    public void Build_WithActionArguments_ProducesEncodedKey(
        IEnumerable<ControllerParameterDescriptor> _,
        RouteValueDictionary routeValues,
        Dictionary<string, object> actionArguments
    )
    {
        var key = new ActionCacheKeyBuilder()
            .WithRouteValues(routeValues)
            .WithActionArguments(actionArguments)
            .Build();

        var decodedKey = new KeyEncoder().Decode(key);

        decodedKey.Should().Be(
            $"{ActionCacheKeyComponents.RouteValuesKey}={CacheJsonSerializer.Serialize(routeValues)}" +
            $"&{ActionCacheKeyComponents.ActionArgumentsKey}={CacheJsonSerializer.Serialize(actionArguments)}"
        );
    }
}

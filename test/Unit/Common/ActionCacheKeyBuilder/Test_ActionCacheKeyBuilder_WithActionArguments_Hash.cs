using Unit.TestUtiltiies.Data;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using ActionCache.Common.Keys;
using ActionCache.Common.Serialization;

namespace Unit.Common;

[TestFixture]
public class ActionCacheKeyBuilderTests
{
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

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
    public void Build_ByDefault_ProducesAnOpaqueKey(
        IEnumerable<ControllerParameterDescriptor> _,
        RouteValueDictionary routeValues,
        Dictionary<string, object?> actionArguments
    )
    {
        // Keys are hashed by default: the components embed every route value and action
        // argument, and a reversible key hands them to anyone who can read the cache.
        var key = new ActionCacheKeyBuilder()
            .WithRouteValues(routeValues)
            .WithActionArguments(actionArguments)
            .Build();

        key.Should().MatchRegex("^[0-9A-F]{64}$", "a SHA-256 hash, not an encoding");

        var decode = () => new KeyEncoder().Decode(key);
        var decoded = decode.Should().NotThrow().Subject;
        decoded.Should().NotContain(ActionCacheKeyComponents.RouteValuesKey,
            "a hashed key must not be recoverable into its components");
    }

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetControllerDescriptors))]
    public void Build_ByDefault_IsDeterministic(
        IEnumerable<ControllerParameterDescriptor> _,
        RouteValueDictionary routeValues,
        Dictionary<string, object?> actionArguments
    )
    {
        string BuildKey() => new ActionCacheKeyBuilder()
            .WithRouteValues(routeValues)
            .WithActionArguments(actionArguments)
            .Build();

        BuildKey().Should().Be(BuildKey());
    }

    [Test]
    public void Build_ByDefault_DiffersWhenTheComponentsDiffer()
    {
        string BuildKey(int id) => new ActionCacheKeyBuilder()
            .WithRouteValues(new RouteValueDictionary { { "controller", "Account" } })
            .WithActionArguments(new Dictionary<string, object?> { { "id", id } })
            .Build();

        BuildKey(1).Should().NotBe(BuildKey(2));
    }

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetControllerDescriptors))]
    public void Build_WithPlaintextKeys_ProducesAReversibleKey(
        IEnumerable<ControllerParameterDescriptor> _,
        RouteValueDictionary routeValues,
        Dictionary<string, object?> actionArguments
    )
    {
        // The debugging escape hatch: readable and reversible, at the cost of exposing
        // every route value and action argument to anyone who can read the cache.
        var key = new ActionCacheKeyBuilder(usePlaintextKeys: true)
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

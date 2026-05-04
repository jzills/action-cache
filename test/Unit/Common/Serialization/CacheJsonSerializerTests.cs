using ActionCache.Common.Serialization;
using Newtonsoft.Json;

namespace Unit.Common.Serialization;

// C1 fix: CacheJsonSerializer previously used TypeNameHandling.All which embedded $type in
// every payload, making every cache value a potential RCE gadget target for anyone with
// cache-write access. Fixed to TypeNameHandling.Auto + SafeSerializationBinder:
//   - Auto: $type is only emitted when the runtime type differs from the declared type
//     (i.e., only for genuine polymorphic scenarios such as IActionResult → OkObjectResult).
//   - SafeSerializationBinder: restricts which types may be instantiated from $type,
//     blocking known gadget-chain namespaces and unloaded assemblies.

[TestFixture]
public class CacheJsonSerializerTests
{
    private sealed record UserDto(string Name, int Age);

    [Test]
    public void Serialize_WithConcreteObjectType_DoesNotEmbedTypeMetadata()
    {
        // Declared type == runtime type → Auto does not emit $type.
        var json = CacheJsonSerializer.Serialize(new UserDto("Alice", 30));

        json.Should().NotContain("$type");
    }

    [Test]
    public void Serialize_WithCollectionType_DoesNotEmbedTypeMetadata()
    {
        var json = CacheJsonSerializer.Serialize(new List<string> { "a", "b" });

        json.Should().NotContain("$type");
    }

    [Test]
    public void Serialize_WithConcreteType_DoesNotLeakTypeName()
    {
        var json = CacheJsonSerializer.Serialize(new UserDto("Alice", 30));

        json.Should().NotContain(nameof(UserDto));
    }

    [Test]
    public void Deserialize_BlockedGadgetChainType_ThrowsJsonSerializationException()
    {
        // An attacker who writes directly to the cache backend might plant a $type that
        // references a known gadget-chain namespace. SafeSerializationBinder must reject it.
        var maliciousPayload = """{"$type":"System.Windows.Data.ObjectDataProvider, PresentationFramework","MethodName":"Start"}""";

        Action act = () => CacheJsonSerializer.Deserialize<object>(maliciousPayload);

        act.Should().Throw<JsonSerializationException>()
            .WithMessage("*Error resolving type*System.Windows*");
    }

    [Test]
    public void Deserialize_ConcreteType_RoundTripsCorrectly()
    {
        var original = new UserDto("Alice", 30);

        var json = CacheJsonSerializer.Serialize(original);
        var result = CacheJsonSerializer.Deserialize<UserDto>(json);

        result.Should().Be(original);
    }
}

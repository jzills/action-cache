using ActionCache.Common.Serialization;
using Newtonsoft.Json;

namespace Unit.Common.Serialization;

// KeyComponentSerializer is the one remaining Newtonsoft path: cache-key reversal needs
// round-trippable types so reflection-based refresh can rebuild action arguments. It keeps
// TypeNameHandling.Auto and SafeSerializationBinder until refresh replays real requests
// instead, at which point the reverse path and this serializer are deleted outright.
// Cache *values* no longer carry type information at all — see CacheJsonSerializerTests.

[TestFixture]
public class KeyComponentSerializerTests
{
    [Test]
    public void Deserialize_BlockedGadgetChainType_ThrowsJsonSerializationException()
    {
        // Someone with cache-write access could plant a $type naming a known gadget chain.
        var maliciousPayload = """{"$type":"System.Windows.Data.ObjectDataProvider, PresentationFramework","MethodName":"Start"}""";

        Action act = () => KeyComponentSerializer.Deserialize<object>(maliciousPayload);

        act.Should().Throw<JsonSerializationException>();
    }

    [Test]
    public void Deserialize_TypeFromAnUnloadedAssembly_ThrowsJsonSerializationException()
    {
        var payload = """{"$type":"Some.Unloaded.Type, NotLoadedAssembly","Value":1}""";

        Action act = () => KeyComponentSerializer.Deserialize<object>(payload);

        act.Should().Throw<JsonSerializationException>();
    }

    [Test]
    public void Serialize_WithConcreteType_DoesNotEmbedTypeMetadata()
    {
        var json = KeyComponentSerializer.Serialize(new Dictionary<string, string?> { ["a"] = "b" });

        json.Should().NotContain("$type");
    }
}

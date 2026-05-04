using ActionCache.Common.Serialization;

namespace Unit.Common.Serialization;

// Bug C1: CacheJsonSerializer uses TypeNameHandling.All in its JsonSerializerSettings.
// This causes the serializer to embed a "$type" property in every non-primitive payload
// and to honor that property on deserialization — instantiating whatever type is named.
//
// If an attacker can write to the cache backend (Redis, SQL Server, Cosmos) they can plant
// a gadget-chain payload and trigger arbitrary code execution on the next deserialization.
//
// Note: Dictionary<string, object> is exempted by the registered ActionArgumentsConverter,
// and JSON primitives (string, int, bool) never carry $type. The real risk is for any
// concrete object or collection used as a cached value type T in SetAsync<T>.
//
// Fix: switch to TypeNameHandling.None and rely on the strongly-typed generic parameter
// T in Deserialize<T>. If polymorphic types are required, use a custom ISerializationBinder
// with an explicit allowlist of safe types.

[TestFixture]
public class CacheJsonSerializerTests
{
    private sealed record UserDto(string Name, int Age);

    [Test]
    public void Serialize_WithConcreteObjectType_EmbedsTypeMetadata_BugC1()
    {
        var dto = new UserDto("Alice", 30);

        var json = CacheJsonSerializer.Serialize(dto);

        // BUG: $type is embedded — an attacker with write access to the cache backend can
        // replace this payload with a gadget-chain type to trigger RCE on deserialization.
        // Fix: TypeNameHandling.None must be used; the fix causes this assertion to pass.
        json.Should().NotContain("$type");
    }

    [Test]
    public void Serialize_WithCollectionType_EmbedsTypeMetadata_BugC1()
    {
        var items = new List<string> { "a", "b", "c" };

        var json = CacheJsonSerializer.Serialize(items);

        // BUG: $type wraps the List — e.g. "$type":"System.Collections.Generic.List`1[...]"
        json.Should().NotContain("$type");
    }

    [Test]
    public void Serialize_WithTypeNameHandlingNone_DoesNotLeakTypeMetadata()
    {
        var dto = new UserDto("Alice", 30);

        var json = CacheJsonSerializer.Serialize(dto);

        json.Should().NotContain(nameof(UserDto));
        json.Should().NotContain("$type");
    }
}

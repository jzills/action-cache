using ActionCache.Common.Serialization;

namespace Unit.Common.Serialization;

// CacheJsonSerializer serializes cache *values*. It is now System.Text.Json with no
// polymorphism at all: responses are stored as a CachedResponse of primitives, so nothing
// in a cached payload can name a type for deserialization to construct. The gadget-chain
// tests that used to live here moved to KeyComponentSerializerTests, which covers the one
// remaining Newtonsoft path (cache-key reversal, deleted when refresh stops reflecting).

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
    public void Deserialize_ConcreteType_RoundTripsCorrectly()
    {
        var original = new UserDto("Alice", 30);

        var json = CacheJsonSerializer.Serialize(original);
        var result = CacheJsonSerializer.Deserialize<UserDto>(json);

        result.Should().Be(original);
    }
}

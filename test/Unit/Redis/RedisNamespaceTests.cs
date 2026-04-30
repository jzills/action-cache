using ActionCache.Redis;
using StackExchange.Redis;

namespace Unit.Redis;

[TestFixture]
public class RedisNamespaceTests
{
    [Test]
    public void ImplicitConversion_FromString_ReturnsRedisNamespace()
    {
        RedisNamespace result = "MyNamespace";

        result.Should().NotBeNull();
        result.Value.Should().Be("MyNamespace");
    }

    [Test]
    public void ImplicitConversion_ToRedisKey_ReturnsRedisKey()
    {
        var redisNamespace = new RedisNamespace("TestNs");

        RedisKey key = redisNamespace;

        key.Should().NotBeNull();
        ((string)key).Should().Contain("TestNs");
    }

    [Test]
    public void Constructor_WithValue_SetsValue()
    {
        var ns = new RedisNamespace("MyValue");

        ns.Value.Should().Be("MyValue");
    }
}

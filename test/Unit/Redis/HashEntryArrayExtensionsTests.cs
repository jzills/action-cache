using ActionCache.Redis;
using ActionCache.Redis.Extensions.Internal;
using StackExchange.Redis;

namespace Unit.Redis;

[TestFixture]
public class HashEntryArrayExtensionsTests
{
    [Test]
    public void GetRedisValue_WhenValueEntryExists_ReturnsValue()
    {
        var entries = new[]
        {
            new HashEntry(RedisHashEntry.Value, "cached-value"),
            new HashEntry(RedisHashEntry.AbsoluteExpiration, 0L),
            new HashEntry(RedisHashEntry.SlidingExpiration, 0L)
        };

        var result = entries.GetRedisValue();

        result.Should().Be((RedisValue)"cached-value");
    }

    [Test]
    public void GetAbsoluteExpiration_WhenEntryExists_ReturnsLongValue()
    {
        var expectedExpiration = 1000L;
        var entries = new[]
        {
            new HashEntry(RedisHashEntry.Value, "val"),
            new HashEntry(RedisHashEntry.AbsoluteExpiration, expectedExpiration),
            new HashEntry(RedisHashEntry.SlidingExpiration, 0L)
        };

        var result = entries.GetAbsoluteExpiration();

        result.Should().Be(expectedExpiration);
    }

    [Test]
    public void GetSlidingExpiration_WhenEntryExists_ReturnsLongValue()
    {
        var expectedSliding = 500L;
        var entries = new[]
        {
            new HashEntry(RedisHashEntry.Value, "val"),
            new HashEntry(RedisHashEntry.AbsoluteExpiration, 0L),
            new HashEntry(RedisHashEntry.SlidingExpiration, expectedSliding)
        };

        var result = entries.GetSlidingExpiration();

        result.Should().Be(expectedSliding);
    }

    [Test]
    public void GetRedisValue_WhenEntryMissing_ThrowsInvalidOperationException()
    {
        var entries = Array.Empty<HashEntry>();

        Action act = () => entries.GetRedisValue();

        act.Should().Throw<InvalidOperationException>();
    }
}

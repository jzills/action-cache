using ActionCache.Common;

namespace Unit.Common;

[TestFixture]
public class ActionCacheEntryOptionsTests
{
    [Test]
    public void GetAbsoluteExpirationFromUtcNow_WhenAbsoluteExpirationIsNull_ReturnsNull()
    {
        var options = new ActionCacheEntryOptions { AbsoluteExpiration = null };
        options.GetAbsoluteExpirationFromUtcNow().Should().BeNull();
    }

    [Test]
    public void GetAbsoluteExpirationFromUtcNow_WhenAbsoluteExpirationIsSet_ReturnsOffsetFromNow()
    {
        var duration = TimeSpan.FromMinutes(10);
        var options = new ActionCacheEntryOptions { AbsoluteExpiration = duration };

        var result = options.GetAbsoluteExpirationFromUtcNow();

        result.Should().NotBeNull();

        // A tolerance, not a before/after bracket: that bracket assumes UtcNow only moves
        // forward, and a host clock adjustment — an NTP resync on WSL2, routinely — steps
        // it either way. What matters is that the offset is measured from now.
        result!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow.Add(duration), TimeSpan.FromSeconds(30));
    }

    [Test]
    public void GetAbsoluteExpirationAsTTLInMilliseconds_WhenNull_ReturnsZero()
    {
        var options = new ActionCacheEntryOptions { AbsoluteExpiration = null };
        options.GetAbsoluteExpirationAsTTLInMilliseconds().Should().Be(0L);
    }

    [Test]
    public void GetAbsoluteExpirationAsTTLInMilliseconds_WhenSet_ReturnsMilliseconds()
    {
        var options = new ActionCacheEntryOptions { AbsoluteExpiration = TimeSpan.FromSeconds(5) };
        options.GetAbsoluteExpirationAsTTLInMilliseconds().Should().Be(5000L);
    }

    [Test]
    public void GetSlidingExpirationInMilliseconds_WhenNull_ReturnsZero()
    {
        var options = new ActionCacheEntryOptions { SlidingExpiration = null };
        options.GetSlidingExpirationInMilliseconds().Should().Be(0L);
    }

    [Test]
    public void GetSlidingExpirationInMilliseconds_WhenSet_ReturnsMilliseconds()
    {
        var options = new ActionCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(3) };
        options.GetSlidingExpirationInMilliseconds().Should().Be(3000L);
    }

    [Test]
    public void GetAbsoluteExpirationFromUtcNowInMilliseconds_WhenNull_ReturnsZero()
    {
        var options = new ActionCacheEntryOptions { AbsoluteExpiration = null };
        options.GetAbsoluteExpirationFromUtcNowInMilliseconds().Should().Be(0L);
    }

    [Test]
    public void GetAbsoluteExpirationFromUtcNowInMilliseconds_WhenSet_ReturnsUnixMilliseconds()
    {
        var options = new ActionCacheEntryOptions { AbsoluteExpiration = TimeSpan.FromMinutes(1) };
        var result = options.GetAbsoluteExpirationFromUtcNowInMilliseconds();
        result.Should().BeGreaterThan(0L);
    }

    [Test]
    public void HasExpirationValue_WhenZero_ReturnsFalse()
    {
        ActionCacheEntryOptions.HasExpirationValue(0L).Should().BeFalse();
    }

    [Test]
    public void HasExpirationValue_WhenPositive_ReturnsTrue()
    {
        ActionCacheEntryOptions.HasExpirationValue(1000L).Should().BeTrue();
    }

    [Test]
    public void HasExpirationValue_WhenNegative_ReturnsFalse()
    {
        ActionCacheEntryOptions.HasExpirationValue(-1L).Should().BeFalse();
    }

    [Test]
    public void HasExpiredAbsoluteExpiration_WhenNoExpiration_ReturnsFalse()
    {
        ActionCacheEntryOptions.HasExpiredAbsoluteExpiration(0L).Should().BeFalse();
    }

    [Test]
    public void HasExpiredAbsoluteExpiration_WhenFuture_ReturnsFalse()
    {
        var future = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds();
        ActionCacheEntryOptions.HasExpiredAbsoluteExpiration(future).Should().BeFalse();
    }

    [Test]
    public void HasExpiredAbsoluteExpiration_WhenPast_ReturnsTrue()
    {
        var past = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
        ActionCacheEntryOptions.HasExpiredAbsoluteExpiration(past).Should().BeTrue();
    }

    [Test]
    public void HasSlidingExpiration_WhenZero_ReturnsFalse()
    {
        ActionCacheEntryOptions.HasSlidingExpiration(0L).Should().BeFalse();
    }

    [Test]
    public void HasSlidingExpiration_WhenPositive_ReturnsTrue()
    {
        ActionCacheEntryOptions.HasSlidingExpiration(5000L).Should().BeTrue();
    }

    [Test]
    public void Deconstruct_WithSlidingExpiration_TtlIsSlidingExpiration()
    {
        var options = new ActionCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromSeconds(10),
            AbsoluteExpiration = TimeSpan.FromMinutes(5)
        };
        options.Deconstruct(out _, out var sliding, out var ttl);
        sliding.Should().Be(10000L);
        ttl.Should().Be(sliding);
    }

    [Test]
    public void Deconstruct_WithOnlyAbsoluteExpiration_TtlIsAbsoluteExpiration()
    {
        var options = new ActionCacheEntryOptions
        {
            AbsoluteExpiration = TimeSpan.FromSeconds(30)
        };
        options.Deconstruct(out _, out var sliding, out var ttl);
        sliding.Should().Be(0L);
        ttl.Should().Be(30000L);
    }

    [Test]
    public void Deconstruct_WithNoExpiration_AllZero()
    {
        var options = new ActionCacheEntryOptions();
        options.Deconstruct(out var abs, out var sliding, out var ttl);
        abs.Should().Be(0L);
        sliding.Should().Be(0L);
        ttl.Should().Be(0L);
    }

    [Test]
    public void DefaultLockTimeout_Is10Seconds()
    {
        new ActionCacheEntryOptions().LockTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Test]
    public void HasSlidingExpiration_IsEquivalentToHasExpirationValue()
    {
        long value = 5_000L;

        bool hasSliding = ActionCacheEntryOptions.HasSlidingExpiration(value);
        bool hasValue = ActionCacheEntryOptions.HasExpirationValue(value);

        hasSliding.Should().Be(hasValue);
    }
}

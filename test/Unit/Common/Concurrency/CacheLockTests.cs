using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;

namespace Unit.Common;

[TestFixture]
public class CacheLockTests
{
    [Test]
    public void CacheLock_DefaultIsAcquired_IsFalse()
    {
        var cacheLock = new SemaphoreSlimLock("res", TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200));
        cacheLock.IsAcquired.Should().BeFalse();
    }

    [Test]
    public void CacheLock_Resource_IsSet()
    {
        var cacheLock = new SemaphoreSlimLock("myResource", TimeSpan.Zero, TimeSpan.Zero);
        cacheLock.Resource.Should().Be("myResource");
    }

    [Test]
    public void CacheLock_DateRequested_IsApproximatelyNow()
    {
        var before = DateTime.UtcNow;
        var cacheLock = new SemaphoreSlimLock("r", TimeSpan.Zero, TimeSpan.Zero);
        var after = DateTime.UtcNow;
        cacheLock.DateRequested.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Test]
    public void SemaphoreSlimLock_Duration_IsSet()
    {
        var cacheLock = new SemaphoreSlimLock("r", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        cacheLock.Duration.Should().Be(TimeSpan.FromSeconds(1));
        cacheLock.Timeout.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void NullCacheLock_Resource_IsSet()
    {
        var cacheLock = new NullCacheLock("resource");
        cacheLock.Resource.Should().Be("resource");
    }

    [Test]
    public void NullCacheLock_IsAcquired_DefaultsFalse()
    {
        var cacheLock = new NullCacheLock("r");
        cacheLock.IsAcquired.Should().BeFalse();
    }

    [Test]
    public void DistributedCacheLock_Key_PrefixedWithLock()
    {
        var cacheLock = new DistributedCacheLock("myns", TimeSpan.Zero, TimeSpan.Zero);
        cacheLock.Key.Should().StartWith("Lock:");
        cacheLock.Key.Should().Contain("myns");
    }

    [Test]
    public void DistributedCacheLock_Value_IsGuid()
    {
        var cacheLock = new DistributedCacheLock("r", TimeSpan.Zero, TimeSpan.Zero);
        cacheLock.Value.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(cacheLock.Value, out _).Should().BeTrue();
    }

    [Test]
    public void DistributedCacheLock_ShouldTryAcquire_WhenNotAcquiredAndNotTimedOut_ReturnsTrue()
    {
        var cacheLock = new DistributedCacheLock("r", TimeSpan.Zero, TimeSpan.FromSeconds(30));
        cacheLock.ShouldTryAcquire().Should().BeTrue();
    }

    [Test]
    public void DistributedCacheLock_ShouldTryAcquire_WhenAlreadyAcquired_ReturnsFalse()
    {
        var cacheLock = new DistributedCacheLock("r", TimeSpan.Zero, TimeSpan.FromSeconds(30));
        cacheLock.IsAcquired = true;
        cacheLock.ShouldTryAcquire().Should().BeFalse();
    }

    [Test]
    public void DistributedCacheLock_HasExceededTimeout_WhenTimeoutIsZero_ReturnsTrue()
    {
        var cacheLock = new DistributedCacheLock("r", TimeSpan.Zero, TimeSpan.Zero);
        cacheLock.HasExceededTimeout().Should().BeTrue();
    }

    [Test]
    public void DistributedCacheLock_HasExceededTimeout_WhenTimeoutIsFuture_ReturnsFalse()
    {
        var cacheLock = new DistributedCacheLock("r", TimeSpan.Zero, TimeSpan.FromSeconds(30));
        cacheLock.HasExceededTimeout().Should().BeFalse();
    }
}

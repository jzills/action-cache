using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;
using ActionCache.Redis.Concurrency.Locks;
using ActionCache.SqlServer.Concurrency.Locks;

namespace Unit.Common;

[TestFixture]
public class CacheLockTests
{
    [Test]
    public void NullCacheLock_Resource_IsSet()
    {
        var cacheLock = new NullCacheLock("resource");
        cacheLock.Resource.Should().Be("resource");
    }

    [Test]
    public void NullCacheLock_IsAcquired_IsTrue()
    {
        var cacheLock = new NullCacheLock("r");
        cacheLock.IsAcquired.Should().BeTrue();
    }

    [Test]
    public void RedisCacheLock_Resource_IsSet()
    {
        var cacheLock = new RedisCacheLock("myns", TimeSpan.FromSeconds(5));
        cacheLock.Resource.Should().Be("myns");
    }

    [Test]
    public void RedisCacheLock_Key_PrefixedWithLock()
    {
        var cacheLock = new RedisCacheLock("myns", TimeSpan.Zero);
        cacheLock.Key.Should().StartWith("Lock:");
        cacheLock.Key.Should().Contain("myns");
    }

    [Test]
    public void RedisCacheLock_Token_IsNonEmptyGuid()
    {
        var cacheLock = new RedisCacheLock("r", TimeSpan.Zero);
        cacheLock.Token.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(cacheLock.Token, out _).Should().BeTrue();
    }

    [Test]
    public void RedisCacheLock_TwoInstances_HaveDifferentTokens()
    {
        var a = new RedisCacheLock("r", TimeSpan.Zero);
        var b = new RedisCacheLock("r", TimeSpan.Zero);
        a.Token.Should().NotBe(b.Token);
    }

    [Test]
    public void RedisCacheLock_IsAcquired_DefaultsFalse()
    {
        var cacheLock = new RedisCacheLock("r", TimeSpan.Zero);
        cacheLock.IsAcquired.Should().BeFalse();
    }

    [Test]
    public void RedisCacheLock_DateRequested_IsApproximatelyNow() =>
        AssertStampedOnConstruction(new RedisCacheLock("r", TimeSpan.Zero));

    [Test]
    public void SqlServerCacheLock_Resource_IsSet()
    {
        var cacheLock = new SqlServerCacheLock("myns", TimeSpan.FromSeconds(5));
        cacheLock.Resource.Should().Be("myns");
    }

    [Test]
    public void SqlServerCacheLock_Timeout_IsSet()
    {
        var cacheLock = new SqlServerCacheLock("r", TimeSpan.FromSeconds(2));
        cacheLock.Timeout.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void SqlServerCacheLock_IsAcquired_DefaultsFalse()
    {
        var cacheLock = new SqlServerCacheLock("r", TimeSpan.Zero);
        cacheLock.IsAcquired.Should().BeFalse();
    }

    [Test]
    public void CacheLock_DateRequested_IsApproximatelyNow() =>
        AssertStampedOnConstruction(new NullCacheLock("r"));

    /// <summary>
    /// Asserts that <see cref="CacheLock.DateRequested"/> was stamped at construction.
    /// </summary>
    /// <param name="cacheLock">The lock to inspect.</param>
    /// <remarks>
    /// Deliberately a tolerance rather than a <c>before &lt;= DateRequested &lt;= after</c>
    /// bracket. That bracket assumes <see cref="DateTime.UtcNow"/> only moves forward, and
    /// it does not: the host clock steps whenever it is adjusted, and on WSL2 an NTP resync
    /// routinely moves it. A run of this test was observed reading a timestamp 153ms
    /// <b>earlier</b> than the one captured on the line above it, which failed the lower
    /// bound while the code under test was behaving perfectly.
    ///
    /// The exactness bought nothing anyway. What the property has to be is stamped at
    /// construction rather than left at <c>default</c>, and a minute of slack still catches
    /// that — <c>default(DateTime)</c> is year 1.
    /// </remarks>
    private static void AssertStampedOnConstruction(CacheLock cacheLock) =>
        cacheLock.DateRequested.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
}

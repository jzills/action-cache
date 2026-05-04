using System.Reflection;
using ActionCache;
using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Redis;
using ActionCache.Utilities;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Unit.Redis;

[TestFixture]
public class RedisActionCacheFactoryTests
{
    private Mock<IConnectionMultiplexer> _multiplexerMock;
    private Mock<IDatabase> _databaseMock;
    private Mock<IActionCacheRefreshProvider> _refreshProviderMock;
    private IOptions<ActionCacheEntryOptions> _entryOptions;
    private RedisActionCacheFactory _sut;

    [SetUp]
    public void SetUp()
    {
        _databaseMock = new Mock<IDatabase>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _refreshProviderMock = new Mock<IActionCacheRefreshProvider>();
        _entryOptions = Options.Create(new ActionCacheEntryOptions());
        _sut = new RedisActionCacheFactory(_multiplexerMock.Object, _entryOptions, _refreshProviderMock.Object);
    }

    [Test]
    public void Create_WithNamespace_ReturnsNonNullCache()
    {
        var result = _sut.Create((Namespace)"TestNs");

        result.Should().NotBeNull();
        result.Should().BeOfType<RedisActionCache>();
    }

    [Test]
    public void Create_WithNamespace_CacheHasCorrectNamespace()
    {
        var result = _sut.Create((Namespace)"TestNs");

        result!.GetNamespace().Value.Should().Be("TestNs");
    }

    [Test]
    public void Create_WithExpirations_ReturnsNonNullCache()
    {
        var result = _sut.Create((Namespace)"TestNs", TimeSpan.FromMinutes(5), null);

        result.Should().NotBeNull();
        result.Should().BeOfType<RedisActionCache>();
    }

    [Test]
    public void Create_WithExpirations_CacheHasCorrectNamespace()
    {
        var result = _sut.Create((Namespace)"TestNs", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1));

        result!.GetNamespace().Value.Should().Be("TestNs");
    }

    [Test]
    public void Create_WithNullExpirations_StillReturnsCache()
    {
        var result = _sut.Create((Namespace)"TestNs", null, null);

        result.Should().NotBeNull();
    }

    // Bug M7: Create(namespace, absoluteExpiration, slidingExpiration) constructs a fresh
    // ActionCacheEntryOptions with only the expiration fields set. LockDuration and LockTimeout
    // from the configured options are not copied, so they silently reset to their defaults
    // (5 s and 10 s respectively). Consumers who configure custom lock durations via IOptions
    // will find them ignored whenever a per-namespace expiration is also specified.
    //
    // Fix: copy LockDuration and LockTimeout from the existing EntryOptions when constructing
    // the replacement, or accept the full ActionCacheEntryOptions and override only expiration.

    [Test]
    public void Create_WithExpirations_DropsConfiguredLockDuration_BugM7()
    {
        var customLockDuration = TimeSpan.FromSeconds(30);
        var configuredOptions = Options.Create(new ActionCacheEntryOptions
        {
            LockDuration = customLockDuration
        });
        var factory = new RedisActionCacheFactory(
            _multiplexerMock.Object,
            configuredOptions,
            _refreshProviderMock.Object);

        var cache = factory.Create((Namespace)"TestNs", TimeSpan.FromMinutes(5), null);

        var entryOptions = GetEntryOptions(cache!);

        // BUG: LockDuration is reset to the default 5 s instead of the configured 30 s.
        entryOptions.LockDuration.Should().Be(customLockDuration);
    }

    [Test]
    public void Create_WithExpirations_DropsConfiguredLockTimeout_BugM7()
    {
        var customLockTimeout = TimeSpan.FromSeconds(60);
        var configuredOptions = Options.Create(new ActionCacheEntryOptions
        {
            LockTimeout = customLockTimeout
        });
        var factory = new RedisActionCacheFactory(
            _multiplexerMock.Object,
            configuredOptions,
            _refreshProviderMock.Object);

        var cache = factory.Create((Namespace)"TestNs", TimeSpan.FromMinutes(5), null);

        var entryOptions = GetEntryOptions(cache!);

        // BUG: LockTimeout is reset to the default 10 s instead of the configured 60 s.
        entryOptions.LockTimeout.Should().Be(customLockTimeout);
    }

    private static ActionCacheEntryOptions GetEntryOptions(IActionCache cache)
    {
        var type = cache.GetType();
        FieldInfo? field = null;
        while (type != null && field == null)
        {
            field = type.GetField("EntryOptions", BindingFlags.NonPublic | BindingFlags.Instance);
            type = type.BaseType;
        }
        return (ActionCacheEntryOptions)field!.GetValue(cache)!;
    }
}

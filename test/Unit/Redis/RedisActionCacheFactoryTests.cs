using System.Reflection;
using ActionCache;
using Microsoft.Extensions.Logging.Abstractions;
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
        _sut = new RedisActionCacheFactory(_multiplexerMock.Object, _entryOptions, _refreshProviderMock.Object, NullLoggerFactory.Instance);
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
    // ActionCacheEntryOptions with only the expiration fields set. LockTimeout from the
    // configured options is not copied, so it silently resets to its 10 s default. Consumers
    // who configure a custom lock timeout via IOptions find it ignored whenever a
    // per-namespace expiration is also specified.
    //
    // Fix: copy LockTimeout from the existing EntryOptions when constructing the
    // replacement, or accept the full ActionCacheEntryOptions and override only expiration.

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
            _refreshProviderMock.Object,
            NullLoggerFactory.Instance);

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

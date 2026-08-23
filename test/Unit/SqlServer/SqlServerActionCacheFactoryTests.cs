using System.Reflection;
using ActionCache;
using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.SqlServer;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Unit.SqlServer;

[TestFixture]
public class SqlServerActionCacheFactoryTests
{
    private Mock<IDistributedCache> _cacheMock;
    private Mock<IActionCacheRefreshProvider> _refreshProviderMock;
    private IOptions<ActionCacheEntryOptions> _entryOptions;
    private IOptions<SqlServerCacheOptions> _sqlServerOptions;
    private SqlServerActionCacheFactory _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _refreshProviderMock = new Mock<IActionCacheRefreshProvider>();
        _entryOptions = Options.Create(new ActionCacheEntryOptions());
        _sqlServerOptions = Options.Create(new SqlServerCacheOptions
        {
            ConnectionString = "Server=localhost;Database=Cache;Integrated Security=true;"
        });
        _sut = new SqlServerActionCacheFactory(
            _cacheMock.Object,
            _sqlServerOptions,
            _entryOptions,
            _refreshProviderMock.Object,
            NullLoggerFactory.Instance);
    }

    [Test]
    public void Create_WithNamespace_ReturnsNonNullCache()
    {
        var result = _sut.Create((Namespace)"TestNs");

        result.Should().NotBeNull();
        result.Should().BeOfType<SqlServerActionCache>();
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
        result.Should().BeOfType<SqlServerActionCache>();
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

    // Bug M7: same as RedisActionCacheFactory — LockTimeout is not copied into the per-call
    // ActionCacheEntryOptions when expiration overrides are provided.
    // Note: SqlServerCacheLocker is initialised with the GLOBAL EntryOptions lock timeout
    // (correct for the locker itself), but the EntryOptions stored inside the cache context
    // is missing the custom value, affecting any future code that reads it from context.

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

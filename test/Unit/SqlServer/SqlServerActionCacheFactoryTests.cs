using ActionCache;
using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.SqlServer;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;

namespace Unit.SqlServer;

[TestFixture]
public class SqlServerActionCacheFactoryTests
{
    private Mock<IDistributedCache> _cacheMock;
    private Mock<IActionCacheRefreshProvider> _refreshProviderMock;
    private IOptions<ActionCacheEntryOptions> _entryOptions;
    private SqlServerActionCacheFactory _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _refreshProviderMock = new Mock<IActionCacheRefreshProvider>();
        _entryOptions = Options.Create(new ActionCacheEntryOptions());
        _sut = new SqlServerActionCacheFactory(_cacheMock.Object, _entryOptions, _refreshProviderMock.Object);
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
}

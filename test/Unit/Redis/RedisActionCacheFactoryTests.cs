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
}

using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;
using ActionCache.Redis;
using ActionCache.Utilities;
using Moq;
using StackExchange.Redis;

namespace Unit.Redis;

[TestFixture]
public class RedisActionCacheExpirationTests
{
    private Mock<IDatabase> _databaseMock;
    private RedisActionCache _sut;

    [SetUp]
    public void SetUp()
    {
        _databaseMock = new Mock<IDatabase>();
        var context = new ActionCacheContext<NullCacheLock>
        {
            Namespace = new Namespace("TestNs"),
            EntryOptions = new ActionCacheEntryOptions(),
            RefreshProvider = new ActionCacheRefreshProvider(new ActionCacheDescriptorProviderNull()),
            CacheLocker = new NullCacheLocker()
        };
        _sut = new RedisActionCache(_databaseMock.Object, context);
    }

    [Test]
    public async Task GetAsync_WhenAbsoluteExpirationHasPassed_DeletesAndReturnsDefault()
    {
        var pastTimestamp = 1L;
        var entries = new HashEntry[]
        {
            new HashEntry(RedisHashEntry.Value, "\"value\""),
            new HashEntry(RedisHashEntry.AbsoluteExpiration, pastTimestamp),
            new HashEntry(RedisHashEntry.SlidingExpiration, 0L)
        };

        _databaseMock.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(entries);
        _databaseMock.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _databaseMock.Setup(db => db.SortedSetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _sut.GetAsync<string>("key");

        result.Should().BeNull();
        _databaseMock.Verify(
            db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Once);
        _databaseMock.Verify(
            db => db.SortedSetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Test]
    public async Task GetAsync_WhenSlidingExpirationSet_RefreshesExpiry()
    {
        var slidingMs = 5000L;
        var entries = new HashEntry[]
        {
            new HashEntry(RedisHashEntry.Value, "\"hello\""),
            new HashEntry(RedisHashEntry.AbsoluteExpiration, 0L),
            new HashEntry(RedisHashEntry.SlidingExpiration, slidingMs)
        };

        _databaseMock.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(entries);
        _databaseMock.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _sut.GetAsync<string>("key");

        result.Should().Be("hello");
        _databaseMock.Verify(
            db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }
}

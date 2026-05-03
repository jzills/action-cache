using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Redis;
using ActionCache.Redis.Concurrency;
using ActionCache.Redis.Concurrency.Locks;
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
        var context = new ActionCacheContext<RedisCacheLock>
        {
            Namespace = new Namespace("TestNs"),
            EntryOptions = new ActionCacheEntryOptions(),
            RefreshProvider = new ActionCacheRefreshProvider(new ActionCacheDescriptorProviderNull()),
            CacheLocker = new RedisCacheLocker(
                _databaseMock.Object,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10))
        };
        _sut = new RedisActionCache(_databaseMock.Object, context);
    }

    [Test]
    public async Task GetAsync_WhenAbsoluteExpirationHasPassed_DeletesAndReturnsDefault()
    {
        var pastTimestamp = 1L; // 1 ms after Unix epoch = year 1970, definitely in the past
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

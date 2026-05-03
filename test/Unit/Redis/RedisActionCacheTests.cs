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
public class RedisActionCacheTests
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
    public async Task GetKeysAsync_Always_RemovesExpiredAndReturnsRemainingKeys()
    {
        _databaseMock.Setup(db => db.SortedSetRemoveRangeByScoreAsync(
            It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0);

        _databaseMock.Setup(db => db.SortedSetRangeByRankAsync(
            It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new RedisValue("key1"), new RedisValue("key2")]);

        var result = await _sut.GetKeysAsync();

        result.Should().BeEquivalentTo(["key1", "key2"]);
    }

    [Test]
    public async Task GetAsync_WhenNoHashEntries_RemovesFromSortedSetAndReturnsDefault()
    {
        _databaseMock.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([]);

        _databaseMock.Setup(db => db.SortedSetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _sut.GetAsync<string>("key");

        result.Should().BeNull();
        _databaseMock.Verify(
            db => db.SortedSetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Test]
    public async Task GetAsync_WhenValidHashEntry_ReturnsDeserializedValue()
    {
        var json = "\"hello\"";
        var entries = new HashEntry[]
        {
            new HashEntry(RedisHashEntry.Value, json),
            new HashEntry(RedisHashEntry.AbsoluteExpiration, 0L),
            new HashEntry(RedisHashEntry.SlidingExpiration, 0L)
        };

        _databaseMock.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(entries);

        var result = await _sut.GetAsync<string>("key");

        result.Should().Be("hello");
    }

    [Test]
    public async Task GetAsync_WhenEmptyJsonValue_ReturnsDefault()
    {
        var entries = new HashEntry[]
        {
            new HashEntry(RedisHashEntry.Value, ""),
            new HashEntry(RedisHashEntry.AbsoluteExpiration, 0L),
            new HashEntry(RedisHashEntry.SlidingExpiration, 0L)
        };

        _databaseMock.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(entries);

        var result = await _sut.GetAsync<string>("key");

        result.Should().BeNull();
    }

    [Test]
    public async Task RemoveAsync_WithKey_UsesLuaScriptWhenAvailable()
    {
        _databaseMock.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        await _sut.RemoveAsync("key");

        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Test]
    public async Task RemoveAsync_NoKey_UsesLuaScriptWhenAvailable()
    {
        _databaseMock.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        await _sut.RemoveAsync();

        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Test]
    public async Task SetAsync_Always_UsesLuaScriptWhenAvailable()
    {
        _databaseMock.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        await _sut.SetAsync("key", "value");

        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }
}

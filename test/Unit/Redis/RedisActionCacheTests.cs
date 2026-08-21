using ActionCache.Common;
using ActionCache.Common.Caching;
using Unit.TestUtilities;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;
using ActionCache.Redis;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
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
        var context = new ActionCacheContext<NullCacheLock>
        {
            Namespace = new Namespace("TestNs"),
            EntryOptions = new ActionCacheEntryOptions(),
            RefreshProvider = NullRefreshProvider.Instance,
            CacheLocker = new NullCacheLocker()
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

    // Bug H4: the primary SetAsync path passes CommandFlags.FireAndForget to ScriptEvaluateAsync.
    // The result (including errors) is discarded entirely — the caller believes the value was
    // cached when it was not. A network error, OOM, or Lua error is silently swallowed.
    // Fix: remove CommandFlags.FireAndForget and await the result so failures are surfaced.

    [Test]
    public async Task SetAsync_WhenLuaScriptLoads_ShouldNotUseFireAndForget_BugH4()
    {
        CommandFlags? capturedFlags = null;
        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, _, _, flags) => capturedFlags = flags)
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        await _sut.SetAsync("key", "value");

        // BUG: capturedFlags is CommandFlags.FireAndForget — write failures are invisible to the caller.
        capturedFlags.Should().NotBe(CommandFlags.FireAndForget);
    }
}

using ActionCache.Common;
using ActionCache.Common.Caching;
using Unit.TestUtilities;
using ActionCache.Common.Concurrency;
using ActionCache.SqlServer;
using ActionCache.SqlServer.Concurrency.Locks;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Unit.SqlServer;

[TestFixture]
public class SqlServerActionCacheTests
{
    private Mock<IDistributedCache> _cacheMock;
    private Mock<ICacheLocker<SqlServerCacheLock>> _lockerMock;
    private SqlServerActionCache _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _lockerMock = new Mock<ICacheLocker<SqlServerCacheLock>>();

        _lockerMock
            .Setup(locker => locker.WaitForLockThenAsync(It.IsAny<string>(), It.IsAny<Func<Task>>()))
            .Returns<string, Func<Task>>((_, func) => func());

        _lockerMock
            .Setup(locker => locker.WaitForLockThenAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<ConcurrentDictionary<string, DateTimeOffset?>>>>()))
            .Returns<string, Func<Task<ConcurrentDictionary<string, DateTimeOffset?>>>>(
                async (_, func) => await func());

        var context = new ActionCacheContext<SqlServerCacheLock>
        {
            Namespace = new Namespace("TestNs"),
            EntryOptions = new ActionCacheEntryOptions(),
            RefreshProvider = NullRefreshProvider.Instance,
            CacheLocker = _lockerMock.Object
        };

        _sut = new SqlServerActionCache(_cacheMock.Object, context);
    }

    [Test]
    public async Task GetAsync_WhenCacheHit_ReturnsDeserializedValue()
    {
        _cacheMock.Setup(cache => cache.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(Encoding.UTF8.GetBytes("\"hello\""));

        var result = await _sut.GetAsync<string>("key");

        result.Should().Be("hello");
    }

    [Test]
    public async Task GetAsync_WhenCacheMiss_ReturnsDefault()
    {
        _cacheMock.Setup(cache => cache.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync((byte[]?)null);

        var result = await _sut.GetAsync<string>("key");

        result.Should().BeNull();
    }

    [Test]
    public async Task GetAsync_WhenEmptyValue_ReturnsDefault()
    {
        _cacheMock.Setup(cache => cache.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(Encoding.UTF8.GetBytes(""));

        var result = await _sut.GetAsync<string>("key");

        result.Should().BeNull();
    }

    [Test]
    public async Task SetAsync_Always_SetsValueInCache()
    {
        _cacheMock.Setup(cache => cache.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(cache => cache.Get(It.IsAny<string>()))
            .Returns((byte[]?)null);

        await _sut.SetAsync("key", "value");

        _cacheMock.Verify(
            cache => cache.SetAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task RemoveAsync_WithKey_RemovesFromCache()
    {
        _cacheMock.Setup(cache => cache.RemoveAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(cache => cache.Get(It.IsAny<string>()))
            .Returns((byte[]?)null);

        await _sut.RemoveAsync("key");

        _cacheMock.Verify(
            cache => cache.RemoveAsync(It.IsAny<string>(), default),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task GetKeysAsync_WhenNoKeys_ReturnsEmpty()
    {
        _cacheMock.Setup(cache => cache.Get(It.IsAny<string>()))
            .Returns((byte[]?)null);

        var result = await _sut.GetKeysAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetKeysAsync_WhenKeysExist_ReturnsKeys()
    {
        var keysDict = new ConcurrentDictionary<string, DateTimeOffset?>();
        keysDict.TryAdd("key1", null);
        keysDict.TryAdd("key2", null);

        _cacheMock.Setup(cache => cache.Get(It.IsAny<string>()))
            .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(keysDict)));

        var result = await _sut.GetKeysAsync();

        result.Should().HaveCount(2);
        result.Should().Contain("key1");
        result.Should().Contain("key2");
    }

    [Test]
    public async Task GetAsync_Namespace_IsCorrect()
    {
        _sut.GetNamespace().Value.Should().Be("TestNs");

        await Task.CompletedTask;
    }
}

using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.SqlServer;
using ActionCache.SqlServer.Concurrency.Locks;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheBaseRefreshTests
{
    private Mock<IDistributedCache> _cacheMock;
    private Mock<ICacheLocker<SqlServerCacheLock>> _lockerMock;
    private Mock<IActionCacheRefreshProvider> _refreshProviderMock;
    private SqlServerActionCache _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _lockerMock = new Mock<ICacheLocker<SqlServerCacheLock>>();
        _refreshProviderMock = new Mock<IActionCacheRefreshProvider>();

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
            RefreshProvider = _refreshProviderMock.Object,
            CacheLocker = _lockerMock.Object
        };

        _sut = new SqlServerActionCache(_cacheMock.Object, context);
    }

    [Test]
    public async Task RefreshAsync_WhenNoKeys_DoesNotSetAnyValues()
    {
        _cacheMock.Setup(cache => cache.Get(It.IsAny<string>()))
            .Returns((byte[]?)null);

        _refreshProviderMock.Setup(provider => provider.GetRefreshResults(
            It.IsAny<Namespace>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new Dictionary<string, object?>());

        await _sut.RefreshAsync();

        _cacheMock.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.Never);
    }

    [Test]
    public async Task RefreshAsync_WhenKeysAndResults_SetsRefreshedValues()
    {
        var keys = new ConcurrentDictionary<string, DateTimeOffset?>();
        keys.TryAdd("key1", null);

        _cacheMock.Setup(cache => cache.Get(It.IsAny<string>()))
            .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(keys)));

        _refreshProviderMock.Setup(provider => provider.GetRefreshResults(
            It.IsAny<Namespace>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new Dictionary<string, object?> { { "key1", "refreshed" } });

        _cacheMock.Setup(cache => cache.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .Returns(Task.CompletedTask);

        await _sut.RefreshAsync();

        _cacheMock.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.AtLeastOnce);
    }
}

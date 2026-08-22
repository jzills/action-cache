using ActionCache.Common.Serialization;
using ActionCache.Memory.Extensions.Internal;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using System.Text;

namespace Unit.SqlServer;

[TestFixture]
public class IDistributedCacheExtensionsTests
{
    private Mock<IDistributedCache> _cacheMock = null!;
    private Namespace _namespace;
    private DistributedCacheEntryOptions _entryOptions = null!;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _namespace = new Namespace("TestNs");
        _entryOptions = new DistributedCacheEntryOptions();
    }

    [Test]
    public async Task GetKeysAsync_WhenCacheReturnsNull_ReturnsEmptyDictionary()
    {
        _cacheMock.Setup(cache => cache.Get((string)_namespace)).Returns((byte[]?)null);

        var result = await _cacheMock.Object.GetKeysAsync(_namespace, _entryOptions);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetKeysAsync_WhenCacheReturnsValidJson_ReturnsDeserializedKeys()
    {
        var json = "{\"mykey\":null}";
        _cacheMock.Setup(cache => cache.Get((string)_namespace)).Returns(Encoding.UTF8.GetBytes(json));

        var result = await _cacheMock.Object.GetKeysAsync(_namespace, _entryOptions);

        result.Should().ContainKey("mykey");
    }

    [Test]
    public async Task GetKeysAsync_WhenCacheReturnsJsonNull_RemovesCacheEntryAndReturnsEmpty()
    {
        _cacheMock.Setup(cache => cache.Get((string)_namespace)).Returns(Encoding.UTF8.GetBytes("null"));

        var result = await _cacheMock.Object.GetKeysAsync(_namespace, _entryOptions);

        result.Should().BeEmpty();
        _cacheMock.Verify(cache => cache.RemoveAsync((string)_namespace, default), Times.Once);
    }

    [Test]
    public async Task SetKeyAsync_WhenKeyNotPresent_StoresUpdatedKeys()
    {
        _cacheMock.Setup(cache => cache.Get(It.IsAny<string>())).Returns((byte[]?)null);

        await _cacheMock.Object.SetKeyAsync(_namespace, "newkey", _entryOptions);

        _cacheMock.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.Once);
    }

    [Test]
    public async Task RemoveKeyAsync_WhenKeyExists_RemovesAndStores()
    {
        var json = "{\"mykey\":null}";
        _cacheMock.Setup(cache => cache.Get((string)_namespace)).Returns(Encoding.UTF8.GetBytes(json));

        await _cacheMock.Object.RemoveKeyAsync(_namespace, "mykey", _entryOptions);

        _cacheMock.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.Once);
    }

    [Test]
    public async Task RemoveKeyAsync_WhenNoKeys_DoesNotStore()
    {
        _cacheMock.Setup(cache => cache.Get(It.IsAny<string>())).Returns((byte[]?)null);

        await _cacheMock.Object.RemoveKeyAsync(_namespace, "nokey", _entryOptions);

        _cacheMock.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.Never);
    }

    [Test]
    public async Task GetKeysAsync_WhenKeysContainExpiredEntries_RemovesExpiredAndUpdatesCache()
    {
        var pastExpiry = DateTimeOffset.UtcNow.AddHours(-1);
        var keys = new System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset?>();
        keys.TryAdd("expiredKey", pastExpiry);
        keys.TryAdd("validKey", DateTimeOffset.UtcNow.AddHours(1));
        var json = CacheJsonSerializer.Serialize(keys);
        _cacheMock.Setup(cache => cache.Get((string)_namespace)).Returns(Encoding.UTF8.GetBytes(json));

        var result = await _cacheMock.Object.GetKeysAsync(_namespace, _entryOptions);

        result.Should().NotContainKey("expiredKey");
        result.Should().ContainKey("validKey");
        _cacheMock.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.Once);
    }
}

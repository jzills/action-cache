using Moq;
using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Utilities;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheHandlerTests
{
    private Mock<IActionCache> _cacheMock;
    private Mock<IActionCache> _nextCacheMock;
    private ActionCacheHandler _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IActionCache>();
        _nextCacheMock = new Mock<IActionCache>();
        _sut = new ActionCacheHandler(_cacheMock.Object);
    }

    [Test]
    public async Task GetAsync_WhenCacheHit_ReturnsPrimaryValue()
    {
        _cacheMock.Setup(cache => cache.GetAsync<string>("key", It.IsAny<CancellationToken>())).ReturnsAsync("cached");

        var result = await _sut.GetAsync<string>("key");

        result.Should().Be("cached");
        _nextCacheMock.Verify(cache => cache.GetAsync<string>("key", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAsync_WhenCacheMissAndNextExists_ReturnsNextValue()
    {
        _cacheMock.Setup(cache => cache.GetAsync<string>("key", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _nextCacheMock.Setup(cache => cache.GetAsync<string>("key", It.IsAny<CancellationToken>())).ReturnsAsync("from-next");
        _sut.SetNext(_nextCacheMock.Object);

        var result = await _sut.GetAsync<string>("key");

        result.Should().Be("from-next");
    }

    [Test]
    public async Task GetAsync_WhenCacheMissAndNoNext_ReturnsNull()
    {
        _cacheMock.Setup(cache => cache.GetAsync<string>("key", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var result = await _sut.GetAsync<string>("key");

        result.Should().BeNull();
    }

    [Test]
    public async Task GetKeysAsync_WhenPrimaryHasKeys_ReturnsPrimaryKeys()
    {
        _cacheMock.Setup(cache => cache.GetKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "k1", "k2" });

        var result = await _sut.GetKeysAsync();

        result.Should().BeEquivalentTo(new[] { "k1", "k2" });
    }

    [Test]
    public async Task GetKeysAsync_WhenPrimaryReturnsNullAndNextExists_ReturnsNextKeys()
    {
        _cacheMock.Setup(cache => cache.GetKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IEnumerable<string>?)null!);
        _nextCacheMock.Setup(cache => cache.GetKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "k3" });
        _sut.SetNext(_nextCacheMock.Object);

        var result = await _sut.GetKeysAsync();

        result.Should().BeEquivalentTo(new[] { "k3" });
    }

    [Test]
    public async Task GetKeysAsync_WhenBothCachesReturnNull_ReturnsEmptyCollection()
    {
        _cacheMock.Setup(cache => cache.GetKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IEnumerable<string>?)null!);

        var result = await _sut.GetKeysAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public void GetNamespace_Always_ReturnsNamespaceFromPrimary()
    {
        var @namespace = new Namespace("TestNs");
        _cacheMock.Setup(cache => cache.GetNamespace()).Returns(@namespace);

        var result = _sut.GetNamespace();

        result.Should().Be(@namespace);
    }

    [Test]
    public async Task RefreshAsync_Always_RefreshesPrimary()
    {
        await _sut.RefreshAsync();

        _cacheMock.Verify(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RefreshAsync_WhenNextExists_RefreshesNext()
    {
        _sut.SetNext(_nextCacheMock.Object);

        await _sut.RefreshAsync();

        _nextCacheMock.Verify(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RefreshAsync_WhenNoNext_DoesNotCallNext()
    {
        await _sut.RefreshAsync();

        _nextCacheMock.Verify(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RemoveAsync_WithKey_RemovesFromPrimary()
    {
        await _sut.RemoveAsync("key");

        _cacheMock.Verify(cache => cache.RemoveAsync("key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveAsync_WithKeyAndNextExists_RemovesFromNext()
    {
        _sut.SetNext(_nextCacheMock.Object);

        await _sut.RemoveAsync("key");

        _nextCacheMock.Verify(cache => cache.RemoveAsync("key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveAsync_NoKey_RemovesAllFromPrimary()
    {
        await _sut.RemoveAsync();

        _cacheMock.Verify(cache => cache.RemoveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveAsync_NoKeyWhenNextExists_RemovesAllFromNext()
    {
        _sut.SetNext(_nextCacheMock.Object);

        await _sut.RemoveAsync();

        _nextCacheMock.Verify(cache => cache.RemoveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SetAsync_Always_SetsToPrimary()
    {
        await _sut.SetAsync("key", "value");

        _cacheMock.Verify(cache => cache.SetAsync("key", "value", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SetAsync_WhenNextExists_SetsToNext()
    {
        _sut.SetNext(_nextCacheMock.Object);

        await _sut.SetAsync("key", "value");

        _nextCacheMock.Verify(cache => cache.SetAsync("key", "value", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void SetNext_Always_ReturnsProvidedNext()
    {
        var returned = _sut.SetNext(_nextCacheMock.Object);

        returned.Should().BeSameAs(_nextCacheMock.Object);
    }

    [Test]
    public void IsNextAvailable_WhenNoNextSet_ReturnsFalse()
    {
        _sut.IsNextAvailable.Should().BeFalse();
    }

    [Test]
    public void IsNextAvailable_WhenNextSet_ReturnsTrue()
    {
        _sut.SetNext(_nextCacheMock.Object);

        _sut.IsNextAvailable.Should().BeTrue();
    }
}

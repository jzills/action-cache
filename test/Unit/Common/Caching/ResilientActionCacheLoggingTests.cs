using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Unit.TestUtilities;

namespace Unit.Common.Caching;

/// <summary>
/// Locks in the event ids, levels, and truthfulness (logged only after the operation
/// succeeds) of the cache-operation diagnostics emitted by <see cref="ResilientActionCache"/>.
/// </summary>
[TestFixture]
public class ResilientActionCacheLoggingTests
{
    private Mock<IActionCache> _inner;
    private CapturingLogger _logger;
    private ResilientActionCache _sut;

    [SetUp]
    public void SetUp()
    {
        _inner = new Mock<IActionCache>();
        _inner.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));
        _logger = new CapturingLogger();
        _sut = new ResilientActionCache(_inner.Object, _logger);
    }

    [Test]
    public async Task GetAsync_WhenValueFound_LogsCacheHit()
    {
        _inner.Setup(cache => cache.GetAsync<string>("key", It.IsAny<CancellationToken>())).ReturnsAsync("value");

        await _sut.GetAsync<string>("key");

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1000);
        entry.Level.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("key").And.Contain("Test");
    }

    [Test]
    public async Task GetAsync_WhenValueMissing_LogsCacheMiss()
    {
        _inner.Setup(cache => cache.GetAsync<string>("key", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await _sut.GetAsync<string>("key");

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1001);
        entry.Level.Should().Be(LogLevel.Debug);
    }

    [Test]
    public async Task SetAsync_WhenInnerSucceeds_LogsCacheSet()
    {
        await _sut.SetAsync("key", "value");

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1002);
        entry.Level.Should().Be(LogLevel.Debug);
    }

    [Test]
    public async Task SetAsync_WhenInnerThrows_LogsDegradationInsteadOfCacheSet()
    {
        _inner.Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("backend down"));

        await _sut.SetAsync("key", "value");

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1006);
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Test]
    public async Task RemoveAsyncAll_WhenInnerSucceeds_LogsCacheEvicted()
    {
        await _sut.RemoveAsync();

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1004);
        entry.Level.Should().Be(LogLevel.Debug);
    }

    [Test]
    public async Task RefreshAsync_WhenInnerSucceeds_LogsCacheRefreshed()
    {
        await _sut.RefreshAsync();

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(1005);
        entry.Level.Should().Be(LogLevel.Debug);
    }
}

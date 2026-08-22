using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Unit.Common.Caching;

[TestFixture]
public class ResilientActionCacheTests
{
    private Mock<IActionCache> _inner = null!;
    private Mock<ILogger> _logger;

    [SetUp]
    public void SetUp()
    {
        _inner = new Mock<IActionCache>();
        _inner.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));
        _logger = new Mock<ILogger>();
        _logger.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private ResilientActionCache CreateSut(bool failClosed = false) =>
        new(_inner.Object, _logger.Object, failClosed);

    private void VerifyWarningLogged(Times times) =>
        _logger.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private void VerifyErrorLogged(Times times) =>
        _logger.Verify(logger => logger.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    [Test]
    public async Task GetAsync_WhenInnerThrows_FailOpen_ReturnsDefaultAndLogsWarning()
    {
        _inner.Setup(cache => cache.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("backend down"));

        var result = await CreateSut().GetAsync<string>("key");

        result.Should().BeNull();
        VerifyWarningLogged(Times.Once());
    }

    [Test]
    public async Task GetAsync_WhenInnerThrows_FailClosed_Rethrows()
    {
        _inner.Setup(cache => cache.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("backend down"));

        var act = async () => await CreateSut(failClosed: true).GetAsync<string>("key");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("backend down");
    }

    [Test]
    public async Task GetAsync_WhenInnerThrows_FailClosed_LogsErrorAndRethrows()
    {
        _inner.Setup(cache => cache.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("backend down"));

        var act = async () => await CreateSut(failClosed: true).GetAsync<string>("key");

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyErrorLogged(Times.Once());
        VerifyWarningLogged(Times.Never());
    }

    [Test]
    public async Task GetKeysAsync_WhenInnerThrows_FailOpen_ReturnsEmpty()
    {
        _inner.Setup(cache => cache.GetKeysAsync(It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var result = await CreateSut().GetKeysAsync();

        result.Should().BeEmpty();
        VerifyWarningLogged(Times.Once());
    }

    [Test]
    public async Task SetAsync_WhenInnerThrows_FailOpen_SwallowsAndLogs()
    {
        _inner.Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut().SetAsync("key", "value");

        await act.Should().NotThrowAsync();
        VerifyWarningLogged(Times.Once());
    }

    [Test]
    public async Task SetAsync_WhenInnerThrows_FailClosed_Rethrows()
    {
        _inner.Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut(failClosed: true).SetAsync("key", "value");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task RemoveAsyncByKey_WhenInnerThrows_FailOpen_Swallows()
    {
        _inner.Setup(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut().RemoveAsync("key");

        await act.Should().NotThrowAsync();
        VerifyWarningLogged(Times.Once());
    }

    [Test]
    public async Task RemoveAsyncAll_WhenInnerThrows_FailClosed_Rethrows()
    {
        _inner.Setup(cache => cache.RemoveAsync(It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut(failClosed: true).RemoveAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task RefreshAsync_WhenInnerThrows_FailOpen_Swallows()
    {
        _inner.Setup(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut().RefreshAsync();

        await act.Should().NotThrowAsync();
        VerifyWarningLogged(Times.Once());
    }

    [Test]
    public async Task GetKeysAsync_WhenInnerThrows_FailClosed_Rethrows()
    {
        _inner.Setup(cache => cache.GetKeysAsync(It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut(failClosed: true).GetKeysAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task RemoveAsyncByKey_WhenInnerThrows_FailClosed_Rethrows()
    {
        _inner.Setup(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut(failClosed: true).RemoveAsync("key");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task RefreshAsync_WhenInnerThrows_FailClosed_Rethrows()
    {
        _inner.Setup(cache => cache.RefreshAsync(It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut(failClosed: true).RefreshAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task HealthyInner_PassesThroughValuesWithoutLogging()
    {
        _inner.Setup(cache => cache.GetAsync<string>("key", It.IsAny<CancellationToken>())).ReturnsAsync("value");
        _inner.Setup(cache => cache.GetKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["a", "b"]);

        var sut = CreateSut();

        (await sut.GetAsync<string>("key")).Should().Be("value");
        (await sut.GetKeysAsync()).Should().BeEquivalentTo(["a", "b"]);
        await sut.SetAsync("key", "value");
        sut.GetNamespace().Should().Be(new Namespace("Test"));

        _inner.Verify(cache => cache.SetAsync("key", "value", It.IsAny<CancellationToken>()), Times.Once);
        VerifyWarningLogged(Times.Never());
    }
}

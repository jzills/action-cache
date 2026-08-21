using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Unit.Common.Caching;

[TestFixture]
public class ResilientCacheDecoratorTests
{
    private static ResilientCacheDecorator CreateSut(bool failClosed = false) =>
        new(NullLoggerFactory.Instance,
            Options.Create(new ActionCacheResilienceOptions { FailClosed = failClosed }));

    [Test]
    public void Decorate_WrapsInnerInResilientActionCache()
    {
        var inner = new Mock<IActionCache>();
        inner.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));

        var result = CreateSut().Decorate(inner.Object);

        result.Should().BeOfType<ResilientActionCache>();
        result.GetNamespace().Should().Be(new Namespace("Test"));
    }

    [Test]
    public async Task Decorate_WhenFailOpen_SwallowsInnerFailure()
    {
        var inner = new Mock<IActionCache>();
        inner.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));
        inner.Setup(cache => cache.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException());

        var result = await CreateSut(failClosed: false).Decorate(inner.Object).GetAsync<string>("key");

        result.Should().BeNull();
    }

    [Test]
    public async Task Decorate_WhenFailClosed_PropagatesInnerFailure()
    {
        var inner = new Mock<IActionCache>();
        inner.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));
        inner.Setup(cache => cache.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException());

        var act = async () => await CreateSut(failClosed: true).Decorate(inner.Object).GetAsync<string>("key");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

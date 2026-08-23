using ActionCache.Memory;
using Microsoft.Extensions.Caching.Memory;

namespace Unit.Common.ExpirationTokens;

[TestFixture]
public class ExpirationTokenSourcesResetTests
{
    private MemoryCache _cache = null!;
    private ExpirationTokenSources _sources = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sources = new ExpirationTokenSources(_cache);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public void Reset_CancelsTheCurrentTokenSource()
    {
        _sources.TryGetOrAdd("Namespace", out var original);

        _sources.Reset("Namespace");

        original.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public void Reset_DoesNotDisposeTheCurrentTokenSource()
    {
        _sources.TryGetOrAdd("Namespace", out var original);

        _sources.Reset("Namespace");

        // A disposed CTS throws ObjectDisposedException on Token — in-flight requests
        // still holding this instance must never see that.
        var read = () => original.Token;
        read.Should().NotThrow();
    }

    [Test]
    public void TryGetOrAdd_AfterReset_ReturnsAFreshUncancelledSource()
    {
        _sources.TryGetOrAdd("Namespace", out var original);
        _sources.Reset("Namespace");

        _sources.TryGetOrAdd("Namespace", out var replacement);

        replacement.Should().NotBeSameAs(original);
        replacement.IsCancellationRequested.Should().BeFalse();
    }

    [Test]
    public void Reset_WhenNamespaceWasNeverUsed_DoesNotThrow()
    {
        var reset = () => _sources.Reset("Unknown");

        reset.Should().NotThrow();
    }

    [Test]
    public void Validated_Reset_WhenKeyIsWhitespace_DoesNotForward()
    {
        var inner = new ExpirationTokenSourcesFallback();
        var validated = new ExpirationTokenSourcesValidated(inner);
        inner.TryGetOrAdd("Real", out var source);

        validated.Reset("   ");

        source.IsCancellationRequested.Should().BeFalse();
    }
}

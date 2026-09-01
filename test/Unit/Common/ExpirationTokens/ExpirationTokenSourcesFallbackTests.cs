using ActionCache.Memory;

namespace Unit.Common;

[TestFixture]
public class ExpirationTokenSourcesFallbackTests
{
    private ExpirationTokenSourcesFallback _fallback = null!;

    [SetUp]
    public void SetUp() => _fallback = new ExpirationTokenSourcesFallback();

    [Test]
    public void TryGetOrAdd_NewKey_ReturnsTrue()
    {
        var result = _fallback.TryGetOrAdd("ns", out var cts);
        result.Should().BeTrue();
        cts.Should().NotBeNull();
    }

    [Test]
    public void TryGetOrAdd_SameKey_ReturnsSameTokenSource()
    {
        _fallback.TryGetOrAdd("ns", out var first);
        _fallback.TryGetOrAdd("ns", out var second);
        first.Should().BeSameAs(second);
    }

    [Test]
    public void TryGetOrAdd_DifferentKeys_ReturnDifferentSources()
    {
        _fallback.TryGetOrAdd("ns1", out var first);
        _fallback.TryGetOrAdd("ns2", out var second);
        first.Should().NotBeSameAs(second);
    }

    [Test]
    public void TryGetOrAdd_CancelledToken_ReturnsNewTokenSource()
    {
        _fallback.TryGetOrAdd("ns", out var original);
        original.Cancel();

        var result = _fallback.TryGetOrAdd("ns", out var replacement);
        result.Should().BeTrue();
        replacement.IsCancellationRequested.Should().BeFalse();
    }
}

using ActionCache.Memory;
using Microsoft.Extensions.Caching.Memory;

namespace Unit.Common;

[TestFixture]
public class ExpirationTokenSourcesValidatedTests
{
    private ExpirationTokenSourcesValidated _validated = null!;

    [SetUp]
    public void SetUp()
    {
        var inner = new ExpirationTokenSources(new MemoryCache(new MemoryCacheOptions()));
        _validated = new ExpirationTokenSourcesValidated(inner);
    }

    [Test]
    public void TryGetOrAdd_ValidKey_DelegatesToNextAndReturnsTrue()
    {
        var result = _validated.TryGetOrAdd("ns", out var cts);
        result.Should().BeTrue();
        cts.Should().NotBeNull();
    }

    [Test]
    public void TryGetOrAdd_NullKey_ReturnsFalse()
    {
        var result = _validated.TryGetOrAdd(null!, out var cts);
        result.Should().BeFalse();
        cts.Should().BeNull();
    }

    [Test]
    public void TryGetOrAdd_EmptyKey_ReturnsFalse()
    {
        var result = _validated.TryGetOrAdd(string.Empty, out var cts);
        result.Should().BeFalse();
        cts.Should().BeNull();
    }

    [Test]
    public void TryGetOrAdd_WhitespaceKey_ReturnsFalse()
    {
        var result = _validated.TryGetOrAdd("   ", out var cts);
        result.Should().BeFalse();
        cts.Should().BeNull();
    }

    [Test]
    public void TryGetOrAdd_SameValidKey_ReturnsSameTokenSource()
    {
        _validated.TryGetOrAdd("ns", out var first);
        _validated.TryGetOrAdd("ns", out var second);
        first.Should().BeSameAs(second);
    }
}

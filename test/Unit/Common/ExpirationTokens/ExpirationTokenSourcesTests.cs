using ActionCache.Memory;
using Microsoft.Extensions.Caching.Memory;

namespace Unit.Common;

[TestFixture]
public class ExpirationTokenSourcesTests
{
    private ExpirationTokenSources _sources = null!;

    [SetUp]
    public void SetUp() =>
        _sources = new ExpirationTokenSources(new MemoryCache(new MemoryCacheOptions()));

    [Test]
    public void TryGetOrAdd_NewKey_ReturnsTrue()
    {
        var result = _sources.TryGetOrAdd("namespace", out var cts);
        result.Should().BeTrue();
        cts.Should().NotBeNull();
    }

    [Test]
    public void TryGetOrAdd_SameKey_ReturnsSameTokenSource()
    {
        _sources.TryGetOrAdd("namespace", out var first);
        _sources.TryGetOrAdd("namespace", out var second);
        first.Should().BeSameAs(second);
    }

    [Test]
    public void TryGetOrAdd_DifferentKeys_ReturnDifferentTokenSources()
    {
        _sources.TryGetOrAdd("ns1", out var first);
        _sources.TryGetOrAdd("ns2", out var second);
        first.Should().NotBeSameAs(second);
    }

    [Test]
    public void EntryOptions_HasCancellationChangeToken()
    {
        _sources.TryGetOrAdd("ns", out var cts);
        var options = _sources.EntryOptions(cts);
        options.ExpirationTokens.Should().HaveCount(1);
    }

    [Test]
    public void EntryOptions_HasSizeOne()
    {
        _sources.TryGetOrAdd("ns", out var cts);
        var options = _sources.EntryOptions(cts);
        options.Size.Should().Be(1);
    }
}

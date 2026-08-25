using ActionCache.Common.Enums;
using ActionCache.Common.Validation;

namespace Unit.Common.Validation;

/// <summary>
/// The rule an endpoint's cache attributes must satisfy, stated once as a pure function over
/// the declarations so every combination can be covered without building a pipeline.
///
/// The rule: an endpoint either caches, or has cache side effects, never both — and no two
/// side effects may name the same namespace.
/// </summary>
[TestFixture]
public class ActionCacheDeclarationConflictTests
{
    private static ActionCacheDeclaration Add(string @namespace) => new(FilterType.Add, @namespace);
    private static ActionCacheDeclaration Evict(string @namespace) => new(FilterType.Evict, @namespace);
    private static ActionCacheDeclaration Refresh(string @namespace) => new(FilterType.Refresh, @namespace);

    [Test]
    public void Detect_WithNoDeclarations_ReturnsNull() =>
        ActionCacheDeclarationConflict.Detect([]).Should().BeNull();

    [Test]
    public void Detect_WithOneOfEach_ReturnsNull()
    {
        ActionCacheDeclarationConflict.Detect([Add("A")]).Should().BeNull();
        ActionCacheDeclarationConflict.Detect([Evict("A")]).Should().BeNull();
        ActionCacheDeclarationConflict.Detect([Refresh("A")]).Should().BeNull();
    }

    [Test]
    public void Detect_WithTwoCaches_ReportsTheDuplicate()
    {
        // Two namespaces for one response has no meaning: a read can only be served from one
        // of them, and which one would come down to filter order.
        var conflict = ActionCacheDeclarationConflict.Detect([Add("A"), Add("B")]);

        conflict.Should().NotBeNull().And.Contain("more than once");
    }

    [TestCase("A", "A", TestName = "Detect_WhenCachingAndEvictingTheSameNamespace_Reports")]
    [TestCase("A", "B", TestName = "Detect_WhenCachingAndEvictingAnotherNamespace_Reports")]
    public void Detect_WithCacheAndEviction_Reports(string cached, string evicted)
    {
        // Rejected whether or not the namespaces differ. The eviction filter runs inside the
        // cache filter, so once the cache starts serving hits the endpoint stops executing and
        // the eviction silently stops happening — correct in development against a cold cache,
        // wrong in production against a warm one.
        var conflict = ActionCacheDeclarationConflict.Detect([Add(cached), Evict(evicted)]);

        conflict.Should().NotBeNull().And.Contain("side effect");
    }

    [TestCase("A", "A", TestName = "Detect_WhenCachingAndRefreshingTheSameNamespace_Reports")]
    [TestCase("A", "B", TestName = "Detect_WhenCachingAndRefreshingAnotherNamespace_Reports")]
    public void Detect_WithCacheAndRefresh_Reports(string cached, string refreshed)
    {
        var conflict = ActionCacheDeclarationConflict.Detect([Add(cached), Refresh(refreshed)]);

        conflict.Should().NotBeNull().And.Contain("side effect");
    }

    [Test]
    public void Detect_WithEvictionAndRefreshOfDifferentNamespaces_ReturnsNull()
    {
        // The combination worth supporting: a write that warms one namespace and clears
        // another. Both fire on the same successful response and neither reads from cache.
        ActionCacheDeclarationConflict.Detect([Refresh("Warm"), Evict("Cold")]).Should().BeNull();
    }

    [Test]
    public void Detect_WithSeveralEvictionsOfDifferentNamespaces_ReturnsNull() =>
        ActionCacheDeclarationConflict.Detect([Evict("A"), Evict("B"), Evict("C")]).Should().BeNull();

    [Test]
    public void Detect_WithEvictionAndRefreshOfTheSameNamespace_ReportsTheNamespace()
    {
        // Contradictory: refresh warms the namespace, eviction empties it, and which one wins
        // is decided by declaration order rather than by anything the author wrote down.
        var conflict = ActionCacheDeclarationConflict.Detect([Refresh("Shared"), Evict("Shared")]);

        conflict.Should().NotBeNull().And.Contain("Shared");
    }

    [Test]
    public void Detect_WithDuplicateEvictionOfOneNamespace_ReportsTheNamespace()
    {
        var conflict = ActionCacheDeclarationConflict.Detect([Evict("A"), Evict("A")]);

        conflict.Should().NotBeNull().And.Contain("A");
    }

    [Test]
    public void Detect_WithCacheConflict_PrefersTheCacheMessageOverTheNamespaceOne()
    {
        // Both rules are broken here. The caching one is reported because it is the one the
        // author must resolve first — removing the duplicate namespace would leave an endpoint
        // that still silently stops evicting.
        var conflict = ActionCacheDeclarationConflict.Detect([Add("A"), Evict("Shared"), Refresh("Shared")]);

        conflict.Should().NotBeNull().And.Contain("side effect");
    }
}

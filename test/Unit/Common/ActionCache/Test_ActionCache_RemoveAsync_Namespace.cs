using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheRemoveNamespaceTests
{
    private IActionCacheFactory _factory;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [Test]
    public async Task RemoveAsync_WhenNamespaceRemoved_AllEntriesAreNull()
    {
        var cache = _factory.Create(nameof(RemoveAsync_WhenNamespaceRemoved_AllEntriesAreNull))!;

        await cache.SetAsync("Foo", "Bar");
        await cache.SetAsync("Biz", "Baz");
        await cache.SetAsync("Coz", "Doz");
        await cache.RemoveAsync();

        var foo = await cache.GetAsync<string>("Foo");
        var biz = await cache.GetAsync<string>("Biz");
        var coz = await cache.GetAsync<string>("Coz");

        foo.Should().BeNull();
        biz.Should().BeNull();
        coz.Should().BeNull();
    }

    [Test]
    public async Task RemoveAsync_WhenRacedWithConcurrentWrites_DoesNotThrowObjectDisposed()
    {
        // Regression: RemoveAsync() disposed a CancellationTokenSource shared with every
        // in-flight request for the namespace, so a concurrent write reading .Token to
        // build its expiration token threw ObjectDisposedException.
        var @namespace = nameof(RemoveAsync_WhenRacedWithConcurrentWrites_DoesNotThrowObjectDisposed);

        var writes = Enumerable.Range(0, 200).Select(index =>
            Task.Run(() => _factory.Create(@namespace)!.SetAsync($"Key:{index}", index)));
        var evictions = Enumerable.Range(0, 20).Select(_ =>
            Task.Run(() => _factory.Create(@namespace)!.RemoveAsync()));

        var race = () => Task.WhenAll(writes.Concat(evictions));

        await race.Should().NotThrowAsync();
    }

    [Test]
    public async Task RemoveAsync_WhenEntriesWereWrittenAfterAPriorEviction_StillRemovesThem()
    {
        // Regression: RemoveAsync() replaced its CancellationTokenSource on the instance
        // only, never writing it back to the store, so entries written afterwards carried
        // a token no later eviction would ever cancel.
        var @namespace = nameof(RemoveAsync_WhenEntriesWereWrittenAfterAPriorEviction_StillRemovesThem);
        var cache = _factory.Create(@namespace)!;

        await cache.SetAsync("Before", "Value");
        await cache.RemoveAsync();

        // Written by the same instance that performed the eviction...
        await cache.SetAsync("After", "Value");

        // ...but evicted by a later request, which resolves the token source from the
        // store rather than from that instance's field.
        await _factory.Create(@namespace)!.RemoveAsync();

        var after = await cache.GetAsync<string>("After");
        after.Should().BeNull();
    }
}

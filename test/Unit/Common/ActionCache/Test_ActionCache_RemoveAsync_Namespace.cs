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
}

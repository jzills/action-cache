using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheSetAsyncTests
{
    private IActionCacheFactory _factory;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [Test]
    public async Task SetAsync_Always_PersistsValue()
    {
        var cache = _factory.Create(nameof(SetAsync_Always_PersistsValue));

        cache.Should().NotBeNull();

        await cache!.SetAsync("Foo", "Bar");
        var result = await cache.GetAsync<string>("Foo");
        await cache.RemoveAsync("Foo");

        result.Should().Be("Bar");
    }
}

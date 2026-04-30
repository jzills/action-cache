using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheRemoveAsyncTests
{
    private IActionCacheFactory _factory;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [Test]
    public async Task RemoveAsync_WhenKeyExists_RemovesEntry()
    {
        var cache = _factory.Create(nameof(RemoveAsync_WhenKeyExists_RemovesEntry))!;

        await cache.SetAsync("Foo", "Bar");
        await cache.RemoveAsync("Foo");

        var result = await cache.GetAsync<string>("Foo");

        result.Should().BeNull();
    }
}

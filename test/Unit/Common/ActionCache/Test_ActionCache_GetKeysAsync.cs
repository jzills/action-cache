using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheGetKeysAsyncTests
{
    private IActionCache _cache;
    private IActionCacheFactory _factory;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [TearDown]
    public async Task TearDown() => await _cache.RemoveAsync();

    [Test]
    public async Task GetKeysAsync_WhenMultipleKeysExist_ReturnsAllKeys()
    {
        _cache = _factory.Create(nameof(GetKeysAsync_WhenMultipleKeysExist_ReturnsAllKeys))!;

        await _cache.SetAsync("Foo", "Bar");
        await _cache.SetAsync("Biz", "Baz");

        var result = await _cache.GetKeysAsync();

        result.Should().HaveCount(2);
    }
}

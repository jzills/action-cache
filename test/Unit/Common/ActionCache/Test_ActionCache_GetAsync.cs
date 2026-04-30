using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheGetAsyncTests
{
    private IActionCache _cache;
    private IActionCacheFactory _factory;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [TearDown]
    public async Task TearDown() => await _cache.RemoveAsync();

    [Test]
    public async Task GetAsync_WhenKeyExists_ReturnsValue()
    {
        _cache = _factory.Create(nameof(GetAsync_WhenKeyExists_ReturnsValue))!;

        await _cache.SetAsync("Foo", "Bar");

        var result = await _cache.GetAsync<string>("Foo");

        result.Should().Be("Bar");
    }

    [Test]
    public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        _cache = _factory.Create(nameof(GetAsync_WhenKeyDoesNotExist_ReturnsNull))!;

        var result = await _cache.GetAsync<int?>("Foo_Not_Present");

        result.Should().BeNull();
    }
}

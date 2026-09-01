using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheGetKeysAsyncTests
{
    private IActionCache _cache = null!;
    private IActionCacheFactory _factory;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [TearDown]
    public async Task TearDown() => await _cache.RemoveAsync();

    [Test]
    public async Task SetAsync_WhenCalledConcurrently_RetainsEveryKeyInTheIndex()
    {
        // Regression: the namespace key index was read-modify-written without a lock, so
        // concurrent writers each built an index and one overwrote the other's keys.
        // Each writer creates its own cache instance, mirroring the per-request creation
        // done by the filter factory — that is what makes a shared locker necessary.
        var @namespace = nameof(SetAsync_WhenCalledConcurrently_RetainsEveryKeyInTheIndex);
        _cache = _factory.Create(@namespace)!;

        await Task.WhenAll(Enumerable.Range(0, 200).Select(index =>
            Task.Run(() => _factory.Create(@namespace)!.SetAsync($"Key:{index}", index))));

        var result = await _cache.GetKeysAsync();

        result.Should().HaveCount(200);
    }

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

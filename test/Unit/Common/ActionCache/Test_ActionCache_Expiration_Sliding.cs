using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheSlidingExpirationTests
{
    private IActionCache _cache;
    private IActionCacheFactory _factory;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [TearDown]
    public async Task TearDown() => await _cache.RemoveAsync();

    [Test]
    public async Task GetAsync_WhenSlidingWindowRefreshed_ReturnsCachedValue()
    {
        _cache = _factory.Create(nameof(GetAsync_WhenSlidingWindowRefreshed_ReturnsCachedValue), slidingExpiration: TimeSpan.FromSeconds(15))!;

        await _cache.SetAsync("Key_Expiration_1", "Value_1");

        var resultBefore = await _cache.GetAsync<string?>("Key_Expiration_1");
        var keysBefore = await _cache.GetKeysAsync();

        resultBefore.Should().Be("Value_1");
        keysBefore.Should().HaveCount(1);

        Thread.Sleep(10000);

        // Access within the sliding window resets the expiry
        await _cache.GetAsync<string?>("Key_Expiration_1");
        await _cache.GetKeysAsync();

        Thread.Sleep(10000);

        var resultAfter = await _cache.GetAsync<string?>("Key_Expiration_1");
        var keysAfter = await _cache.GetKeysAsync();

        resultAfter.Should().NotBeNull();
        keysAfter.Should().HaveCount(1);
    }
}

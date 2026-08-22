using ActionCache;
using Unit.TestUtilities;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheSlidingExpirationTests
{
    private IActionCache _cache = null!;
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

        // Clock deadlines rather than Task.Delay: this test only means something if ten
        // seconds of *wall* clock really pass before the touch, and the host clock does not
        // always agree with an interval. See WallClock.
        await WallClock.WaitUntilPast(DateTimeOffset.UtcNow.AddSeconds(10));

        // Access within the sliding window resets the expiry
        await _cache.GetAsync<string?>("Key_Expiration_1");
        await _cache.GetKeysAsync();

        var touchedAt = DateTimeOffset.UtcNow;
        await WallClock.WaitUntilPast(touchedAt.AddSeconds(10));

        var resultAfter = await _cache.GetAsync<string?>("Key_Expiration_1");
        var keysAfter = await _cache.GetKeysAsync();

        // If the clock jumped forward past the whole window the entry expired legitimately
        // and the failure is the environment's, not the cache's — say which.
        (DateTimeOffset.UtcNow - touchedAt).Should().BeLessThan(TimeSpan.FromSeconds(15),
            "the wall clock must stay inside the sliding window for this assertion to mean anything");

        resultAfter.Should().NotBeNull();
        keysAfter.Should().HaveCount(1);
    }
}

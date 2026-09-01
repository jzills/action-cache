using ActionCache;
using Integration.TestUtilities;
using Integration.TestUtilities.Data;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_Expiration_Sliding
{
    IActionCache? Cache;

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test_GetAsync_Expires(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        Cache = cacheFactory.Create(nameof(Test_GetAsync_Expires), slidingExpiration: TimeSpan.FromSeconds(30));

        await Cache!.SetAsync("Key_Expiration_1", "Value_1");
        var result = await Cache!.GetAsync<string?>("Key_Expiration_1");
        var keys = await Cache!.GetKeysAsync();

        Assert.That(result, Is.EqualTo("Value_1"));
        Assert.That(keys.Count(), Is.EqualTo(1));

        // Clock deadlines rather than sleeps: the entry only proves the window slid if ten
        // seconds of wall clock really passed before the touch. See WallClock.
        await WallClock.WaitUntilPast(DateTimeOffset.UtcNow.AddSeconds(10));

        // The touch inside the window is what resets the expiry.
        result = await Cache!.GetAsync<string?>("Key_Expiration_1");
        keys = await Cache!.GetKeysAsync();

        var touchedAt = DateTimeOffset.UtcNow;
        await WallClock.WaitUntilPast(touchedAt.AddSeconds(10));

        result = await Cache!.GetAsync<string?>("Key_Expiration_1");
        keys = await Cache!.GetKeysAsync();

        // A clock that jumped past the whole window would expire the entry legitimately;
        // name that rather than reporting it as a caching failure.
        Assert.That(DateTimeOffset.UtcNow - touchedAt, Is.LessThan(TimeSpan.FromSeconds(30)),
            "the wall clock must stay inside the sliding window for this assertion to mean anything");

        Assert.That(result, Is.Not.Null);
        Assert.That(keys.Count(), Is.EqualTo(1));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (Cache != null)
            await Cache.RemoveAsync();
    }
}

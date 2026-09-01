using ActionCache;
using Integration.TestUtilities;
using Integration.TestUtilities.Data;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_Expiration_Absolute
{
    IActionCache? Cache;

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test_GetAsync_Expires(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        Cache = cacheFactory.Create(nameof(Test_GetAsync_Expires), TimeSpan.FromSeconds(5));

        await Cache!.SetAsync("Key_Expiration_1", "Value_1");

        // Captured after the write, so the entry expires at or before this point. A clock
        // deadline rather than a sleep — see WallClock — plus the same headroom the original
        // sleep had: TTL enforcement is eventual on Cosmos and the key-index sweeps are lazy,
        // so passing the expiry instant by a hair is not enough.
        var expiredWell = DateTimeOffset.UtcNow.AddSeconds(5).AddSeconds(5);

        var result = await Cache!.GetAsync<string?>("Key_Expiration_1");
        var keys = await Cache!.GetKeysAsync();

        Assert.That(result, Is.EqualTo("Value_1"));
        Assert.That(keys.Count(), Is.EqualTo(1));

        await WallClock.WaitUntilPast(expiredWell);

        result = await Cache!.GetAsync<string?>("Key_Expiration_1");
        keys = await Cache!.GetKeysAsync();

        Assert.That(result, Is.Null);
        Assert.That(keys.Count(), Is.EqualTo(0));
    }

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test_GetKeys_Expires(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        Cache = cacheFactory.Create(nameof(Test_GetKeys_Expires), TimeSpan.FromSeconds(5));

        await Cache!.SetAsync("Key_Expiration_1", "Value_1");

        // Captured after the write, so the entry expires at or before this point. A clock
        // deadline rather than a sleep — see WallClock — plus the same headroom the original
        // sleep had: TTL enforcement is eventual on Cosmos and the key-index sweeps are lazy,
        // so passing the expiry instant by a hair is not enough.
        var expiredWell = DateTimeOffset.UtcNow.AddSeconds(5).AddSeconds(5);

        var result = await Cache!.GetAsync<string?>("Key_Expiration_1");
        var keys = await Cache!.GetKeysAsync();

        Assert.That(result, Is.EqualTo("Value_1"));
        Assert.That(keys.Count(), Is.EqualTo(1));

        await WallClock.WaitUntilPast(expiredWell);

        keys = await Cache!.GetKeysAsync();
        result = await Cache!.GetAsync<string?>("Key_Expiration_1");

        Assert.That(result, Is.Null);
        Assert.That(keys.Count(), Is.EqualTo(0));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (Cache != null)
            await Cache.RemoveAsync();
    }
}

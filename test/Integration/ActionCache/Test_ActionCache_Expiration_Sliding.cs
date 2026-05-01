using ActionCache;
using Integration.TestUtilities.Data;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_Expiration_Sliding
{
    IActionCache Cache;

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test_GetAsync_Expires(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        Cache = cacheFactory.Create(nameof(Test_GetAsync_Expires), slidingExpiration: TimeSpan.FromSeconds(30));

        await Cache.SetAsync("Key_Expiration_1", "Value_1");
        var result = await Cache.GetAsync<string?>("Key_Expiration_1");
        var keys = await Cache.GetKeysAsync();

        Assert.That(result, Is.EqualTo("Value_1"));
        Assert.That(keys.Count(), Is.EqualTo(1));

        Thread.Sleep(10000);

        result = await Cache.GetAsync<string?>("Key_Expiration_1");
        keys = await Cache.GetKeysAsync();

        Thread.Sleep(10000);

        result = await Cache.GetAsync<string?>("Key_Expiration_1");
        keys = await Cache.GetKeysAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(keys.Count(), Is.EqualTo(1));
    }

    [TearDown]
    public async Task TearDown()
    {
        await Cache.RemoveAsync();
    }
}

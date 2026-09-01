using ActionCache;
using Integration.TestUtilities.Data;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_RemoveAsync_Namespace
{
    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create(nameof(Test_ActionCache_RemoveAsync_Namespace))!;

        await cache.SetAsync("Foo", "Bar");
        await cache.SetAsync("Biz", "Baz");
        await cache.SetAsync("Coz", "Doz");
        await cache.RemoveAsync();

        string?[] result = [
            await cache.GetAsync<string>("Foo"),
            await cache.GetAsync<string>("Biz"),
            await cache.GetAsync<string>("Coz")
        ];

        Assert.That(result, Is.All.Null);
    }
}

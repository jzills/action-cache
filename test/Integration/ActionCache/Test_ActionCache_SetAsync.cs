using ActionCache;
using Integration.TestUtilities.Data;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_SetAsync
{
    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create(nameof(Test_ActionCache_SetAsync));

        Assert.That(cache, Is.Not.Null);

        await cache.SetAsync("Foo", "Bar");
        var result = await cache.GetAsync<string>("Foo");
        await cache.RemoveAsync("Foo");

        Assert.That(result, Is.EqualTo("Bar"));
    }
}

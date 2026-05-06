using ActionCache;
using Integration.TestUtilities.Data;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_GetAsync
{
    IActionCache? Cache;

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        Cache = cacheFactory.Create(nameof(Test_ActionCache_GetAsync))!;
        await Cache.SetAsync("Foo", "Bar");

        var result = await Cache.GetAsync<string>("Foo");
        Assert.That(result, Is.EqualTo("Bar"));
    }

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test_NullableInt_ReturnsNull(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        Cache = cacheFactory.Create("Test")!;

        var result = await Cache.GetAsync<int?>("Foo_Not_Present");
        Assert.That(result, Is.EqualTo(null));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (Cache != null)
            await Cache.RemoveAsync();
    }
}

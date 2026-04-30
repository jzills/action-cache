using ActionCache;
using Integration.TestUtilities.Data;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_GetKeysAsync
{
    IActionCache Cache;

    [Test]
    [TestCaseSource(typeof(TestData), nameof(TestData.GetServiceProviders))]
    public async Task Test(IServiceProvider serviceProvider)
    {
        var cacheFactory = serviceProvider.GetRequiredService<IActionCacheFactory>();
        Cache = cacheFactory.Create(nameof(Test_ActionCache_GetKeysAsync))!;

        await Cache.SetAsync("Foo", "Bar");
        await Cache.SetAsync("Biz", "Baz");

        var result = await Cache.GetKeysAsync();
        Assert.That(result.Count(), Is.EqualTo(2));
    }

    [TearDown]
    public async Task TearDown()
    {
        await Cache.RemoveAsync();
    }
}

using System.Reflection;
using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_CacheStatus_Add
{
    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMvc()
            .AddApplicationPart(Assembly.GetExecutingAssembly());
        builder.Services.AddActionCache(options => options.UseRedisCache("127.0.0.1:6379"));

        App = builder.Build();
        App.UseHttpsRedirection();
        App.UseRouting();
        App.MapControllers();

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    [Test]
    public async Task FirstRequest_ReportsAdd_AndPopulatesTheCache()
    {
        var first = await Client.GetAsync("/users");
        first.EnsureSuccessStatusCode();

        Assert.That(first.Headers.GetValues(CacheHeaders.CacheStatus).First(),
            Is.EqualTo(nameof(CacheStatus.Add)), "an uncached request stores the response");

        // Populating is the claim worth checking — the entry has to be readable afterwards.
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var keys = await cacheFactory.Create("Users")!.GetKeysAsync();

        Assert.That(keys.Count(), Is.EqualTo(1));

        var second = await Client.GetAsync("/users");
        second.EnsureSuccessStatusCode();
        Assert.That(second.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Hit)));
        Assert.That(await second.Content.ReadAsStringAsync(), Is.EqualTo(await first.Content.ReadAsStringAsync()));
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create("Users");
        await cache!.RemoveAsync();
        await App.StopAsync();
    }
}
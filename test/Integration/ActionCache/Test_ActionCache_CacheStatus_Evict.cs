using System.Reflection;
using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_Eviction
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
    public async Task Eviction_RemovesTheEntry_SoTheNextRequestRepopulatesIt()
    {
        var first = await Client.GetAsync("/users");
        first.EnsureSuccessStatusCode();
        var originalBody = await first.Content.ReadAsStringAsync();

        var hit = await Client.GetAsync("/users");
        hit.EnsureSuccessStatusCode();
        Assert.That(hit.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Hit)));

        var eviction = await Client.DeleteAsync("/users");
        eviction.EnsureSuccessStatusCode();
        Assert.That(eviction.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Evict)));

        // The header only says eviction ran. This says the entry is gone: a repopulating
        // request reports Add, not Hit.
        var afterEviction = await Client.GetAsync("/users");
        afterEviction.EnsureSuccessStatusCode();
        Assert.That(afterEviction.Headers.GetValues(CacheHeaders.CacheStatus).First(),
            Is.EqualTo(nameof(CacheStatus.Add)), "the evicted entry must no longer be served from cache");
        Assert.That(await afterEviction.Content.ReadAsStringAsync(), Is.EqualTo(originalBody));
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
using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.EndpointFilters.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheEvictionEndpointFilter
{
    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        App = builder.Build();
        App.UseHttpsRedirection();
        App.UseRouting();
        App.MapGet("/teams", () => new { Id = 1, Value = "Joshua" })
            .WithActionCache("Teams");
        App.MapDelete("/teams", () => { })
            .WithActionCacheEviction("Teams");

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    [Test]
    public async Task Eviction_RemovesTheEntry_SoTheNextRequestRepopulatesIt()
    {
        const string route = "/teams";

        var first = await Client.GetAsync(route);
        first.EnsureSuccessStatusCode();

        var hit = await Client.GetAsync(route);
        hit.EnsureSuccessStatusCode();
        Assert.That(hit.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Hit)));

        var eviction = await Client.DeleteAsync(route);
        eviction.EnsureSuccessStatusCode();
        Assert.That(eviction.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Evict)));

        // The header only reports that eviction ran; this proves the entry is gone.
        var afterEviction = await Client.GetAsync(route);
        afterEviction.EnsureSuccessStatusCode();
        Assert.That(afterEviction.Headers.GetValues(CacheHeaders.CacheStatus).First(),
            Is.EqualTo(nameof(CacheStatus.Add)), "the evicted entry must no longer be served from cache");
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create("Teams");
        await cache!.RemoveAsync();
        await App.StopAsync();
    }
}
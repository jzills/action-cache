using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.EndpointFilters.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheRefreshEndpointFilter
{
    WebApplication App;
    HttpClient Client;

    // The value the cached endpoint returns. Mutated by the refreshing endpoint so a
    // refreshed entry can be told apart from the one recorded before it.
    static int Version;

    [SetUp]
    public async Task Setup()
    {
        Version = 1;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        App = builder.Build();
        App.UseHttpsRedirection();
        App.UseRouting();
        App.MapGet("/players", () => new { Id = 1, Version })
            .WithActionCache("Players");
        App.MapPost("/players", () =>
            {
                Version++;
                return Results.Ok();
            })
            .WithActionCacheRefresh("Players");

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    [Test]
    public async Task Refresh_ReplacesTheEntry_SoTheNextRequestIsStillServedFromCache()
    {
        const string route = "/players";

        var first = await Client.GetAsync(route);
        first.EnsureSuccessStatusCode();
        Assert.That(await first.Content.ReadAsStringAsync(), Does.Contain("\"version\":1"));

        var hit = await Client.GetAsync(route);
        Assert.That(hit.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Hit)));

        var refresh = await Client.PostAsync(route, content: null);
        refresh.EnsureSuccessStatusCode();
        Assert.That(refresh.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Refresh)));

        var afterRefresh = await Client.GetAsync(route);
        afterRefresh.EnsureSuccessStatusCode();

        // This pair is the whole point of refresh over eviction. Still a Hit, so the entry
        // was replaced rather than dropped -- eviction would report Add here. And the body
        // is the new version, so what was replaced is the fresh response, not the stale one.
        Assert.That(afterRefresh.Headers.GetValues(CacheHeaders.CacheStatus).First(),
            Is.EqualTo(nameof(CacheStatus.Hit)), "refresh must leave the cache warm, not empty");
        Assert.That(await afterRefresh.Content.ReadAsStringAsync(), Does.Contain("\"version\":2"),
            "the entry served must be the replayed response, not the one recorded before the refresh");
    }

    [Test]
    public async Task Refresh_WithNothingCached_Succeeds()
    {
        // Nothing has populated the namespace, so the refresh walks an empty key set. It
        // must still report success rather than failing the write it is attached to.
        var refresh = await Client.PostAsync("/players", content: null);

        refresh.EnsureSuccessStatusCode();
        Assert.That(refresh.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Refresh)));
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create("Players");
        await cache!.RemoveAsync();
        await App.StopAsync();
    }
}

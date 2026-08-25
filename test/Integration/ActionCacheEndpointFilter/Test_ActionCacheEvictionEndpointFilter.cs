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
        App.MapGet("/players", () => new { Id = 1, Value = "Players" })
            .WithActionCache("Players");
        App.MapGet("/coaches", () => new { Id = 2, Value = "Coaches" })
            .WithActionCache("Coaches");
        App.MapDelete("/roster", () => { })
            .WithActionCacheEviction("Players")
            .WithActionCacheEviction("Coaches");

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

    [Test]
    public async Task Eviction_ChainedForTwoNamespaces_ClearsBoth()
    {
        // Each WithActionCacheEviction call must evict the namespace it was given. When both
        // filters read their namespace back out of endpoint metadata, GetMetadata<T>() hands
        // each of them the *last* attribute registered, so the first namespace is silently
        // never evicted while the second is evicted twice.
        var first = await Client.GetAsync("/players");
        first.EnsureSuccessStatusCode();
        var second = await Client.GetAsync("/coaches");
        second.EnsureSuccessStatusCode();

        var eviction = await Client.DeleteAsync("/roster");
        eviction.EnsureSuccessStatusCode();

        var afterFirst = await Client.GetAsync("/players");
        var afterSecond = await Client.GetAsync("/coaches");

        Assert.Multiple(() =>
        {
            Assert.That(afterFirst.Headers.GetValues(CacheHeaders.CacheStatus).First(),
                Is.EqualTo(nameof(CacheStatus.Add)), "the first chained namespace must be evicted");
            Assert.That(afterSecond.Headers.GetValues(CacheHeaders.CacheStatus).First(),
                Is.EqualTo(nameof(CacheStatus.Add)), "the second chained namespace must be evicted");
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        await cacheFactory.Create("Teams")!.RemoveAsync();
        await cacheFactory.Create("Players")!.RemoveAsync();
        await cacheFactory.Create("Coaches")!.RemoveAsync();
        await App.StopAsync();
    }
}
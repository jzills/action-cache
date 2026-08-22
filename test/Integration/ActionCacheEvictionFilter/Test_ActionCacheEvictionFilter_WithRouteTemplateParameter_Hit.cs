using System.Reflection;
using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheEvictionFilter_WithRouteTemplateParameter
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
    public async Task Eviction_RemovesTheTargetedResource_AndLeavesOthersAlone()
    {
        // Route-templated namespaces are the headline feature: evicting Teams:{id} for one
        // account must not touch another's entries. The previous version of this test
        // deleted with a freshly generated Guid, so it evicted a namespace that had never
        // held anything and asserted only that the Evict header appeared — it would have
        // passed with eviction disabled entirely.
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await PrimeCacheAsync(accountA, teamId);
        await PrimeCacheAsync(accountB, teamId);

        Assert.That(await GetCacheStatusAsync(accountA, teamId), Is.EqualTo(nameof(CacheStatus.Hit)));
        Assert.That(await GetCacheStatusAsync(accountB, teamId), Is.EqualTo(nameof(CacheStatus.Hit)));

        // The eviction route binds {id} from the last segment, so this targets Teams:accountA.
        var eviction = await Client.DeleteAsync($"{accountA}/teams/{teamId}/{accountA}");
        eviction.EnsureSuccessStatusCode();
        Assert.That(eviction.Headers.GetValues(CacheHeaders.CacheStatus).First(),
            Is.EqualTo(nameof(CacheStatus.Evict)));

        Assert.That(await GetCacheStatusAsync(accountA, teamId), Is.EqualTo(nameof(CacheStatus.Add)),
            "the evicted account's entry must be gone, so the next request repopulates it");
        Assert.That(await GetCacheStatusAsync(accountB, teamId), Is.EqualTo(nameof(CacheStatus.Hit)),
            "evicting one account must not disturb another's cached entries");
    }

    private async Task PrimeCacheAsync(Guid accountId, Guid teamId)
    {
        var response = await Client.GetAsync($"{accountId}/teams/{teamId}");
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> GetCacheStatusAsync(Guid accountId, Guid teamId)
    {
        var response = await Client.GetAsync($"{accountId}/teams/{teamId}");
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues(CacheHeaders.CacheStatus).First();
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
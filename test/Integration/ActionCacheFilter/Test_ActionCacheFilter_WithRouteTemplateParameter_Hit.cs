using System.Reflection;
using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheFilter_WithRouteTemplateParameter
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
    public async Task Hit_ServesEachResourcesOwnResponse()
    {
        var accountId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var firstA = await Client.GetAsync($"{accountId}/teams/{teamA}");
        firstA.EnsureSuccessStatusCode();
        var bodyA = await firstA.Content.ReadAsStringAsync();

        var firstB = await Client.GetAsync($"{accountId}/teams/{teamB}");
        firstB.EnsureSuccessStatusCode();
        var bodyB = await firstB.Content.ReadAsStringAsync();

        Assert.That(bodyA, Is.Not.EqualTo(bodyB), "the two teams must not share a cache entry");

        var hitA = await Client.GetAsync($"{accountId}/teams/{teamA}");
        hitA.EnsureSuccessStatusCode();

        Assert.That(hitA.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Hit)));
        Assert.That(await hitA.Content.ReadAsStringAsync(), Is.EqualTo(bodyA),
            "a hit must return the entry for the requested team, not another one");
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
using ActionCache.Common.Enums;
using System.Reflection;
using ActionCache;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheRefreshFilter_WithRouteTemplateParameter
{
    WebApplication App;
    HttpClient Client;
    Guid AccountId = Guid.NewGuid();

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
    public async Task Refresh_OverManyEntries_LeavesEachServingItsOwnResponse()
    {
        // The previous version cached ten entries and asserted the key count — it never
        // triggered a refresh, so it exercised GetKeysAsync rather than refresh.
        var teamIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();

        var expected = new Dictionary<Guid, string>();
        foreach (var teamId in teamIds)
        {
            var response = await Client.GetAsync($"{AccountId}/teams/{teamId}");
            response.EnsureSuccessStatusCode();
            expected[teamId] = await response.Content.ReadAsStringAsync();
        }

        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create($"Teams:{AccountId}")!;
        Assert.That((await cache.GetKeysAsync()).Count(), Is.EqualTo(10));

        // The refresh route binds {id} from the last segment, so this targets Teams:{AccountId}.
        var refresh = await Client.PutAsync($"{AccountId}/teams/{teamIds[0]}/{AccountId}", null);
        refresh.EnsureSuccessStatusCode();
        Assert.That(refresh.Headers.GetValues(CacheHeaders.CacheStatus).First(),
            Is.EqualTo(nameof(CacheStatus.Refresh)));

        Assert.That((await cache.GetKeysAsync()).Count(), Is.EqualTo(10),
            "refreshing must not drop entries");

        // Each entry must still serve its own response. Replaying ten requests through one
        // namespace is exactly where a cross-entry mix-up would show.
        foreach (var teamId in teamIds)
        {
            var response = await Client.GetAsync($"{AccountId}/teams/{teamId}");
            response.EnsureSuccessStatusCode();

            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(expected[teamId]),
                $"the entry for team {teamId} must still be its own response after a refresh");
        }
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create($"Teams:{AccountId}");
        await cache!.RemoveAsync();
        await App.StopAsync();
    }
}
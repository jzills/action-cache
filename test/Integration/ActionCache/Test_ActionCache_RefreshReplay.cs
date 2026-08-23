using System.Reflection;
using ActionCache;
using ActionCache.Common.Extensions;
using Integration.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_RefreshReplay
{
    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        UsersController.RefreshableValue = "original";

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMvc().AddApplicationPart(Assembly.GetExecutingAssembly());
        builder.Services.AddActionCache(options => options.UseRedisCache("127.0.0.1:6379"));

        App = builder.Build();
        App.UseRouting();
        App.MapControllers();

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    [Test]
    public async Task Refresh_ReplaysTheRecordedRequestAndUpdatesTheEntry()
    {
        // Prime the cache.
        var first = await Client.GetStringAsync("/users/refreshable");
        Assert.That(first, Does.Contain("original"));

        // Change the source data. The cached entry is now stale.
        UsersController.RefreshableValue = "updated";
        var stale = await Client.GetStringAsync("/users/refreshable");
        Assert.That(stale, Does.Contain("original"), "the entry should still be cached and stale");

        // Refreshing replays the recorded GET, which runs the action against the new data.
        var refreshResponse = await Client.PostAsync("/users/refreshable", null);
        refreshResponse.EnsureSuccessStatusCode();

        var refreshed = await Client.GetStringAsync("/users/refreshable");
        Assert.That(refreshed, Does.Contain("updated"),
            "refresh must re-issue the request and store the current response");
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create("Replay");
        await cache!.RemoveAsync();
        await App.StopAsync();
    }
}

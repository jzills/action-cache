using System.Net.Http.Json;
using System.Reflection;
using ActionCache;
using ActionCache.Common.Extensions;
using Integration.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_RefreshReplay_WithBody
{
    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        UsersController.RefreshableBodyValue = "original";

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
    public async Task Refresh_ReplaysTheRecordedBody_AndActuallyUpdatesTheEntry()
    {
        var query = new Query { ShowAll = true };

        var first = await Client.PostAsJsonAsync("users/query-refreshable", query);
        first.EnsureSuccessStatusCode();
        Assert.That(await first.Content.ReadAsStringAsync(), Does.Contain("original"));

        // Change the source data; the cached entry is now stale.
        UsersController.RefreshableBodyValue = "updated";

        var refresh = await Client.PostAsync("users/query-refreshable/refresh", null);
        refresh.EnsureSuccessStatusCode();

        var afterRefresh = await Client.PostAsJsonAsync("users/query-refreshable", query);
        var body = await afterRefresh.Content.ReadAsStringAsync();

        Assert.That((int)afterRefresh.StatusCode, Is.EqualTo(200),
            "the entry must not have been replaced by a binding error");
        Assert.That(body, Does.Contain("updated"),
            "refresh must replay the recorded body so the action rebinds and produces current data");
        Assert.That(body, Does.Contain("true"),
            "the replayed body must carry the original query values, not defaults");
    }

    [TearDown]
    public async Task TearDown()
    {
        var factory = App.Services.GetRequiredService<IActionCacheFactory>();
        await factory.Create("BodyReplay")!.RemoveAsync();
        await App.StopAsync();
    }
}

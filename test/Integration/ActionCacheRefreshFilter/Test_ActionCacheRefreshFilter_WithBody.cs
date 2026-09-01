using System.Net.Http.Json;
using System.Reflection;
using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using Integration.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheRefreshFilter_WithBody
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
    public async Task Test()
    {
        var query = new Query
        {
            IncludeIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            ShowAll = true,
            SubQueries = [new SubQuery { Contains = "Test Contains" }]
        };

        var response = await Client.PostAsJsonAsync("users/query", query);
        response.EnsureSuccessStatusCode();
        var cachedBody = await response.Content.ReadAsStringAsync();

        response = await Client.PostAsync("users", null);
        response.EnsureSuccessStatusCode();

        Assert.That(response.Headers.Contains(CacheHeaders.CacheStatus));
        Assert.That(response.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(Enum.GetName(CacheStatus.Refresh)));

        // Regression: refresh replays the recorded request. Without the body, the replay
        // bound nothing, the endpoint answered 415, and that error was written back over a
        // working entry. Asserting only the refresh call's status header missed it entirely.
        var afterRefresh = await Client.PostAsJsonAsync("users/query", query);

        Assert.That((int)afterRefresh.StatusCode, Is.EqualTo(200),
            "refresh must not replace a good entry with an error");
        Assert.That(await afterRefresh.Content.ReadAsStringAsync(), Is.EqualTo(cachedBody));
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
using ActionCache;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheRefreshFilter_WithRouteTemplateParameter
{
    WebApplication? App;
    HttpClient? Client;
    Guid AccountId = Guid.NewGuid();

    [SetUp]
    public void Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMvc();
        builder.Services.AddActionCache(options => options.UseRedisCache("127.0.0.1:6379"));

        var app = builder.Build();
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseEndpoints(options => options.MapControllers());

        App = app;
        Client = app.GetTestClient();
    }

    [Test]
    public async Task Test()
    {
        var teamIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid());
        var teamTasks = teamIds.Select(teamId => Client!.GetAsync($"{AccountId}/teams/{teamId}"));
        await Task.WhenAll(teamTasks);

        var cacheFactory = App!.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create($"Teams:{AccountId}");
        var keys = await cache!.GetKeysAsync();
        Assert.That(keys.Count(), Is.EqualTo(10));
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App!.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create($"Teams:{AccountId}");
        await cache!.RemoveAsync();
    }
}
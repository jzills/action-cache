using System.Reflection;
using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.EndpointFilters.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheEndpointFilter_Hit
{
    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMvc() // Required dependency ActionCacheDescriptorProvider -> Fix this to use EndpointDataSource ??
            .AddApplicationPart(Assembly.GetExecutingAssembly());
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        App = builder.Build();
        App.UseHttpsRedirection();
        App.UseRouting();
        App.MapGet("/teams/{id}", () => new { Id = 1, Value = "Joshua" })
            .WithActionCache("Teams");

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    [Test]
    public async Task Hit_ServesTheSameResponseThatWasCached()
    {
        var first = await Client.GetAsync("/teams/1");
        first.EnsureSuccessStatusCode();
        var originalBody = await first.Content.ReadAsStringAsync();

        var second = await Client.GetAsync("/teams/1");
        second.EnsureSuccessStatusCode();

        Assert.That(second.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Hit)));
        Assert.That(await second.Content.ReadAsStringAsync(), Is.EqualTo(originalBody));
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
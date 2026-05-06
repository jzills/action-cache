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
    public async Task Test()
    {
        var route = "teams/1";
        var response = await Client.GetAsync(route);
        response.EnsureSuccessStatusCode();

        response = await Client.GetAsync(route);
        response.EnsureSuccessStatusCode();

        Assert.That(response.Headers.Contains(CacheHeaders.CacheStatus));
        Assert.That(response.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(Enum.GetName(CacheStatus.Hit)));
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
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
        App.UseEndpoints(options => options.MapControllers());

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    [Test]
    public async Task Test()
    {
        var route = $"{Guid.NewGuid()}/teams/{Guid.NewGuid()}";
        var response = await Client.GetAsync(route);
        response.EnsureSuccessStatusCode();

        response = await Client.DeleteAsync($"{route}/{Guid.NewGuid()}");
        response.EnsureSuccessStatusCode();

        Assert.That(response.Headers.Contains(CacheHeaders.CacheStatus));
        Assert.That(response.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(Enum.GetName(CacheStatus.Evict)));
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
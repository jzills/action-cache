using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_CacheStatus_Hit
{
    WebApplication? App;
    HttpClient? Client;

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
        var response = await Client!.GetAsync("/users");
        response.EnsureSuccessStatusCode();

        // Cache hit
        response = await Client.GetAsync("/users");
        response.EnsureSuccessStatusCode();

        Assert.That(response.Headers.Contains(CacheHeaders.CacheStatus));
        Assert.That(response.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(Enum.GetName(CacheStatus.Hit)));
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App!.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create("Users");
        await cache!.RemoveAsync();
    }
}
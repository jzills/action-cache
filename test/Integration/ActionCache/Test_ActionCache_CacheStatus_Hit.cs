using System.Reflection;
using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_CacheStatus_Hit
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
    public async Task Hit_ServesTheSameResponseThatWasCached()
    {
        var first = await Client.GetAsync("/users");
        first.EnsureSuccessStatusCode();
        var originalBody = await first.Content.ReadAsStringAsync();

        var second = await Client.GetAsync("/users");
        second.EnsureSuccessStatusCode();

        Assert.That(second.Headers.GetValues(CacheHeaders.CacheStatus).First(), Is.EqualTo(nameof(CacheStatus.Hit)));

        // A Hit header proves the lookup succeeded; this proves it returned the right thing.
        Assert.That(await second.Content.ReadAsStringAsync(), Is.EqualTo(originalBody));
        Assert.That(second.Content.Headers.ContentType?.MediaType,
            Is.EqualTo(first.Content.Headers.ContentType?.MediaType),
            "a cached response must reproduce its content type");
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
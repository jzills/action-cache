using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.EndpointFilters.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCacheEndpointFilter_Hit
{
    TestServer Server;
    HttpClient Client;

    [SetUp]
    public void Setup()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services => 
            {
                services.AddMvc(); // Required dependency ActionCacheDescriptorProvider -> Fix this to use EndpointDataSource ??
                services.AddRouting();
                services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));
            })
            .Configure(app =>
            {
                app.UseHttpsRedirection();
                app.UseRouting();
                app.UseEndpoints(options =>
                {
                    options.MapGet("/teams/{id}", () => new { Id = 1, Value = "Joshua" })
                        .WithActionCache("Teams");
                });
            });

        Server = new TestServer(builder);
        Client = Server.CreateClient();
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
        var cacheFactory = Server.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create("Teams");
        await cache!.RemoveAsync();
    }
}
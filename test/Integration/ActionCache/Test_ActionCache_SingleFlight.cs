using System.Reflection;
using ActionCache;
using ActionCache.Common.Extensions;
using Integration.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class Test_ActionCache_SingleFlight
{
    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        UsersController.SingleFlightInvocations = 0;

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
    public async Task ConcurrentRequestsToOneCachedAction_InvokeTheActionOnce()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            Client.GetAsync("/users/single-flight")));

        foreach (var response in responses)
        {
            response.EnsureSuccessStatusCode();
        }

        Assert.That(UsersController.SingleFlightInvocations, Is.EqualTo(1));
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create("SingleFlight");
        await cache!.RemoveAsync();
        await App.StopAsync();
    }
}
